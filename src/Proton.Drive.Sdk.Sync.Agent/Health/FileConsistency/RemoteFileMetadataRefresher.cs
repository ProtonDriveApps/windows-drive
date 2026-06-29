using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MoreLinq.Extensions;
using Proton.Drive.Sdk.Sync.Adapter.Trees.Adapter;
using Proton.Drive.Sdk.Sync.DataAccess.Databases;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.Health;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Agent.Health.FileConsistency;

internal sealed class RemoteFileMetadataRefresher
{
    private const int BatchSize = 500;

    private readonly FileConsistencyGuardDatabase _database;
    private readonly ITransactedScheduler _remoteAdapterSyncScheduler;
    private readonly RemoteAdapterDatabase _remoteAdapterDatabase;
    private readonly ILogger<RemoteFileMetadataRefresher> _logger;

    private readonly Dictionary<long, int> _folderIdToRootIdMap = new(capacity: BatchSize / 10);

    private bool _isFirstRun = true;

    public RemoteFileMetadataRefresher(
        FileConsistencyGuardDatabase database,
        ITransactedScheduler remoteAdapterSyncScheduler,
        RemoteAdapterDatabase remoteAdapterDatabase,
        ILogger<RemoteFileMetadataRefresher> logger)
    {
        _database = database;
        _remoteAdapterSyncScheduler = remoteAdapterSyncScheduler;
        _remoteAdapterDatabase = remoteAdapterDatabase;
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        var startTimestamp = Stopwatch.GetTimestamp();

        var processedFiles = await RefreshMetadataAsync(cancellationToken).ConfigureAwait(false);

        if (processedFiles == 0)
        {
            _logger.LogDebug("File consistency guard: No files require remote metadata refresh");
            return false;
        }

        _logger.LogInformation(
            "File consistency guard: Refreshed metadata of {NumberOfFiles} remote files in {ElapsedTime}",
            processedFiles,
            Stopwatch.GetElapsedTime(startTimestamp));

        return true;
    }

    private static void MarkSkipped(FileConsistencyGuardFileModel file, FileConsistencyGuardFileReason reason)
    {
        file.Status = FileConsistencyGuardFileStatus.Skipped;
        file.Reason = reason;
        file.Error = FileConsistencyGuardFileError.None;
    }

    private async Task<int> RefreshMetadataAsync(CancellationToken cancellationToken)
    {
        var files =
            (await _database.FileRepository
                .GetFilesByStatusesAsync([FileConsistencyGuardFileStatus.None], includeDisabledRoots: _isFirstRun)
                .ConfigureAwait(false))
            .ToList();

        if (files.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation("File consistency guard: Refreshing metadata of remote {NumberOfFiles} files", files.Count);

        foreach (var fileBatch in files.Batch(BatchSize))
        {
            await ProcessBatchAsync(fileBatch, cancellationToken).ConfigureAwait(false);

            await UpdateFilesAsync(fileBatch).ConfigureAwait(false);

            // Small delay after each batch to reduce CPU and I/O pressure
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        _isFirstRun = false;
        return files.Count;
    }

    private async Task ProcessBatchAsync(ICollection<FileConsistencyGuardFileModel> files, CancellationToken cancellationToken)
    {
        _folderIdToRootIdMap.Clear();

        using (await _remoteAdapterSyncScheduler.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            _remoteAdapterSyncScheduler.ForceCommit = true;

            RefreshMetadata();
        }

        return;

        void RefreshMetadata()
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var node = _remoteAdapterDatabase.AdapterTreeRepository.NodeById(file.Id);

                if (node?.Type is not NodeType.File || node.AltId.IsDefault() || string.IsNullOrEmpty(node.AltId.ItemId) || node.IsDirtyPlaceholder())
                {
                    // Remote file is deleted
                    MarkSkipped(file, FileConsistencyGuardFileReason.Deleted);
                    continue;
                }

                if (file.ContentVersion != node.ContentVersion)
                {
                    // Remote file content changed
                    MarkSkipped(file, FileConsistencyGuardFileReason.Updated);
                    continue;
                }

                if (file.RemoteId != node.AltId.ItemId || file.RevisionId != node.RevisionId)
                {
                    // Remote file or revision identity changed without updating content version
                    file.RemoteId = node.AltId.ItemId;
                    file.RevisionId = node.RevisionId;
                    file.RemoteSize = node.Size;
                    file.RemoteLastWriteTime = node.LastWriteTime;
                    file.RemoteHash = null;

                    file.Status = FileConsistencyGuardFileStatus.None;
                    file.Reason = FileConsistencyGuardFileReason.None;
                    file.Error = FileConsistencyGuardFileError.None;
                }

                if (file.RemoteHash == null && file.Status is not FileConsistencyGuardFileStatus.Inconsistent)
                {
                    file.RemoteRootId = GetRootId(node);
                }
            }
        }
    }

    private int GetRootId(AdapterTreeNodeModel<long, string> fileNode)
    {
        var node = fileNode;

        while (node.ParentId != 0)
        {
            if (_folderIdToRootIdMap.TryGetValue(node.ParentId, out var cachedRootId))
            {
                if (node.Type is NodeType.Directory)
                {
                    _folderIdToRootIdMap[node.Id] = cachedRootId;
                }

                return cachedRootId;
            }

            node = _remoteAdapterDatabase.AdapterTreeRepository.NodeById(node.ParentId)
                ?? throw new InvalidOperationException("Unable to locate the Sync Root node");
        }

        if (!int.TryParse(node.Name, out var rootId))
        {
            throw new InvalidOperationException($"Failed to parse sync root ID from name \"{node.Name}\"");
        }

        _folderIdToRootIdMap[node.Id] = rootId;

        return rootId;
    }

    private async Task UpdateFilesAsync(ICollection<FileConsistencyGuardFileModel> files)
    {
        await _database.FileRepository.UpdateFilesAsync(files).ConfigureAwait(false);
    }
}
