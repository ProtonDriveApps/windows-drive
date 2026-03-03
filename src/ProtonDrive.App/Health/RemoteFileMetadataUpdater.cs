using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MoreLinq.Extensions;
using ProtonDrive.App.Settings;
using ProtonDrive.Client.Health;
using ProtonDrive.DataAccess.Databases;
using ProtonDrive.Shared.Telemetry;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Health;

namespace ProtonDrive.App.Health;

internal sealed class RemoteFileMetadataUpdater
{
    // Indicates that remote file hash is missing in extended attributes
    public const string MissingHashIndicator = "missing";

    private const int BatchSize = 100;

    private readonly FileConsistencyGuardDatabase _database;
    private readonly Dictionary<int, RemoteToLocalMapping> _mappingsByRootId;
    private readonly IRemoteFileMetadataProvider _metadataProvider;
    private readonly IErrorCounter _errorCounter;
    private readonly ILogger<RemoteFileMetadataUpdater> _logger;

    public RemoteFileMetadataUpdater(
        FileConsistencyGuardDatabase database,
        IReadOnlyCollection<RemoteToLocalMapping> mappings,
        IRemoteFileMetadataProvider metadataProvider,
        IErrorCounter errorCounter,
        ILogger<RemoteFileMetadataUpdater> logger)
    {
        _database = database;
        _mappingsByRootId = mappings.ToDictionary(x => x.Id);
        _metadataProvider = metadataProvider;
        _errorCounter = errorCounter;
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            var processedFiles = await UpdateMetadataAsync(cancellationToken).ConfigureAwait(false);

            if (processedFiles == 0)
            {
                _logger.LogDebug("File consistency guard: No files require remote metadata update");
                return false;
            }

            _logger.LogInformation(
                "File consistency guard: Updated metadata of {NumberOfFiles} remote files in {ElapsedTime}",
                processedFiles,
                Stopwatch.GetElapsedTime(startTimestamp));

            return true;
        }
        catch (FileSystemClientException ex) when (ex.ErrorCode is FileSystemErrorCode.Offline)
        {
            // Skip processing when offline
            return true;
        }
    }

    private static void SetMetadata(FileConsistencyGuardFileModel file, string sha1Digest, long sizeOnStorage)
    {
        file.Status = FileConsistencyGuardFileStatus.None;
        file.Reason = FileConsistencyGuardFileReason.None;
        file.Error = FileConsistencyGuardFileError.None;
        file.RemoteHash = sha1Digest;
        file.RemoteSizeOnStorage = sizeOnStorage;

        file.UpdateStatus();
    }

    private static void MarkDiverged(FileConsistencyGuardFileModel file)
    {
        file.Status = FileConsistencyGuardFileStatus.None;
        file.Reason = FileConsistencyGuardFileReason.None;
        file.Error = FileConsistencyGuardFileError.RemoteDiverged;
    }

    private static void MarkRootDisabled(FileConsistencyGuardFileModel file)
    {
        file.Status = FileConsistencyGuardFileStatus.None;
        file.Reason = FileConsistencyGuardFileReason.None;
        file.Error = FileConsistencyGuardFileError.RootDisabled;
    }

    private static void MarkFailed(FileConsistencyGuardFileModel file)
    {
        file.Status = FileConsistencyGuardFileStatus.None;
        file.Reason = FileConsistencyGuardFileReason.None;
        file.Error = FileConsistencyGuardFileError.RemoteFailed;
    }

    private async Task<int> UpdateMetadataAsync(CancellationToken cancellationToken)
    {
        var files = (await _database.FileRepository
                .GetFilesByStatusAsync(FileConsistencyGuardFileStatus.None, includeDisabledRoots: false)
                .ConfigureAwait(false))
            .Where(x => x.LocalHash is not null && (x.RemoteHash is null || x.RemoteSizeOnStorage is null))
            .ToList();

        if (files.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation("File consistency guard: Updating metadata of {NumberOfFiles} remote files", files.Count);

        var totalSucceeded = 0;
        var totalFailed = 0;

        foreach (var filesByRootGroup in files.GroupBy(x => x.RemoteRootId))
        {
            var rootId = filesByRootGroup.Key;
            var filesByRoot = filesByRootGroup.ToList();

            if (!_mappingsByRootId.TryGetValue(rootId, out var mapping))
            {
                var failedCount = filesByRoot.Count;
                _logger.LogWarning(
                    "File consistency guard: Skipping {FileCount} files for root {RootId}: Mapping not found",
                    failedCount,
                    rootId);

                _errorCounter.Add(ErrorScope.DataIntegrityItemOperation, new FileConsistencyGuardException(FileConsistencyGuardErrorCode.MappingNotFound));

                await MarkAsFailedAndUpdateAsync(filesByRoot).ConfigureAwait(false);

                totalFailed += failedCount;
                continue;
            }

            if (!mapping.HasSetupSucceeded)
            {
                var failedCount = filesByRoot.Count;
                _logger.LogWarning(
                    "File consistency guard: Skipping {FileCount} files for root {RootId}: Mapping setup failed",
                    failedCount,
                    rootId);

                _errorCounter.Add(ErrorScope.DataIntegrityItemOperation, new FileConsistencyGuardException(FileConsistencyGuardErrorCode.MappingSetupFailed));

                await MarkAsRootDisabledAndUpdateAsync(filesByRoot).ConfigureAwait(false);

                totalFailed += failedCount;
                continue;
            }

            var shareId = mapping.Remote.ShareId;

            if (string.IsNullOrEmpty(shareId))
            {
                var failedCount = filesByRoot.Count;
                _logger.LogWarning(
                    "File consistency guard: Skipping {FileCount} files for root {RootId}: ShareId is empty",
                    failedCount,
                    rootId);

                _errorCounter.Add(ErrorScope.DataIntegrityItemOperation, new FileConsistencyGuardException(FileConsistencyGuardErrorCode.RemoteShareEmpty));

                await MarkAsFailedAndUpdateAsync(filesByRoot).ConfigureAwait(false);

                totalFailed += failedCount;
                continue;
            }

            foreach (var fileBatch in filesByRoot.Batch(BatchSize))
            {
                var processed = await ProcessBatchAsync(fileBatch, shareId, cancellationToken).ConfigureAwait(false);

                totalSucceeded += processed.Success;
                totalFailed += processed.Failed;

                var progress = (totalSucceeded + totalFailed) * 100 / files.Count;

                if (progress % 10 == 0 || totalSucceeded + totalFailed == files.Count)
                {
                    _logger.LogInformation(
                        "File consistency guard: Progress {Progress}% ({Processed}/{Total})",
                        progress,
                        totalSucceeded + totalFailed,
                        files.Count);
                }

                // Small delay after each batch to reduce API and I/O pressure
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation(
            "File consistency guard: Completed metadata update (Succeeded: {Succeeded}, Failed: {Failed})",
            totalSucceeded,
            totalFailed);

        return files.Count;
    }

    private async Task<(int Success, int Failed)> ProcessBatchAsync(
        FileConsistencyGuardFileModel[] files,
        string shareId,
        CancellationToken cancellationToken)
    {
        var remoteIds = files.Select(x => x.RemoteId).ToList();

        var remoteNodes = await _metadataProvider.GetFileMetadataAsync(remoteIds, shareId, cancellationToken).ConfigureAwait(false);
        var remoteNodesById = remoteNodes.ToDictionary(x => x.Id!, x => x);

        var succeededCount = 0;
        var failedCount = 0;

        foreach (var file in files)
        {
            if (!remoteNodesById.TryGetValue(file.RemoteId, out var nodeInfo))
            {
                // API didn't return a node or node decryption failed
                MarkDiverged(file);
                _errorCounter.Add(ErrorScope.DataIntegrityItemOperation, new FileConsistencyGuardException(FileConsistencyGuardErrorCode.FileDiverged));
                failedCount++;
                continue;
            }

            succeededCount++;

            // When the RevisionId is available, a change to it is interpreted as a file content change.
            // Remote files, discovered by the app version 1.4.0 and earlier, have no RevisionId (it's NULL) in the Adapter Tree.
            if (file.RevisionId is not null && file.RevisionId != nodeInfo.RevisionId)
            {
                MarkDiverged(file);
                _errorCounter.Add(ErrorScope.DataIntegrityItemOperation, new FileConsistencyGuardException(FileConsistencyGuardErrorCode.FileDiverged));
                continue;
            }

            // When the RevisionId is not available, a change to LastWriteTime or Size is interpreted as a file content change, except when
            // the previous LastWriteTime has default value.
            // Local files always have LastWriteTime and Size defined, but SizeOnStorage and RevisionId are null.
            // Remote files, discovered by the app version 1.4.1 and earlier, have no LastWriteTime value (it's default) in the Adapter Tree.
            // Remote files, discovered by the app version 1.4.2 and later, have LastWriteTime value in the Adapter Tree. When the ModificationTime
            // is missing in remote file extended attributes, the remote link modification time is used.
            // Incoming remote file node models always have RevisionId, LastWriteTime, and SizeOnStorage values, Size is optional.
            // When the Size is missing in remote file extended attributes, SizeOnStorage is used.
            if (file.RevisionId is null)
            {
                var size = nodeInfo.Size >= 0 ? nodeInfo.Size : nodeInfo.SizeOnStorage ?? 0L;

                if ((file.RemoteLastWriteTime != default && nodeInfo.LastWriteTimeUtc != file.RemoteLastWriteTime) ||
                    (size != file.RemoteSize && nodeInfo.SizeOnStorage != file.RemoteSize))
                {
                    MarkDiverged(file);
                    _errorCounter.Add(ErrorScope.DataIntegrityItemOperation, new FileConsistencyGuardException(FileConsistencyGuardErrorCode.FileDiverged));
                    continue;
                }
            }

            SetMetadata(file, nodeInfo.Sha1Digest ?? MissingHashIndicator, nodeInfo.SizeOnStorage ?? 0L);
        }

        await UpdateFilesAsync(files).ConfigureAwait(false);

        return (succeededCount, failedCount);
    }

    private async Task MarkAsRootDisabledAndUpdateAsync(ICollection<FileConsistencyGuardFileModel> files)
    {
        foreach (var file in files)
        {
            MarkRootDisabled(file);
        }

        await UpdateFilesAsync(files).ConfigureAwait(false);
    }

    private async Task MarkAsFailedAndUpdateAsync(ICollection<FileConsistencyGuardFileModel> files)
    {
        foreach (var file in files)
        {
            MarkFailed(file);
        }

        await UpdateFilesAsync(files).ConfigureAwait(false);
    }

    private async Task UpdateFilesAsync(ICollection<FileConsistencyGuardFileModel> files)
    {
        await _database.FileRepository.UpdateFilesAsync(files).ConfigureAwait(false);
    }
}
