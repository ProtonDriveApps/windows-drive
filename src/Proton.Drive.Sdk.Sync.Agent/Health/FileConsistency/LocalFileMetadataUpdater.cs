using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using MoreLinq.Extensions;
using Proton.Drive.Sdk.Sync.DataAccess.Databases;
using Proton.Drive.Sdk.Sync.Shared.Adapters;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Health;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.IO;
using Proton.Drive.Shared.Telemetry;

namespace Proton.Drive.Sdk.Sync.Agent.Health.FileConsistency;

internal sealed class LocalFileMetadataUpdater
{
    // Indicates that remote file hash is missing in extended attributes
    public const string PartialFileIndicator = "partial";

    private const int BatchSize = 20;
    private const double ThrottleDelayCoefficient = 2.0;
    private static readonly TimeSpan ThrottleSamplingInterval = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan MaxThrottleDelay = TimeSpan.FromMinutes(5);

    private readonly FileConsistencyGuardDatabase _database;
    private readonly IFileRevisionProvider<long> _fileRevisionProvider;
    private readonly IErrorCounter _errorCounter;
    private readonly ILogger<LocalFileMetadataUpdater> _logger;

    private readonly DateTime _notApplicableIfCreatedAfter;

    private bool _isFirstRun = true;

    public LocalFileMetadataUpdater(
        AppConfig appConfig,
        FileConsistencyGuardDatabase database,
        IFileRevisionProvider<long> fileRevisionProvider,
        IErrorCounter errorCounter,
        ILogger<LocalFileMetadataUpdater> logger)
    {
        _database = database;
        _fileRevisionProvider = fileRevisionProvider;
        _errorCounter = errorCounter;
        _logger = logger;

        _notApplicableIfCreatedAfter = appConfig.FileConsistencyGuardNotApplicableSince.UtcDateTime;
    }

    public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        var startTimestamp = Stopwatch.GetTimestamp();

        var processedFiles = await UpdateMetadataAsync(cancellationToken).ConfigureAwait(false);

        if (processedFiles == 0)
        {
            _logger.LogDebug("File consistency guard: No files require local metadata update");
            return false;
        }

        _logger.LogInformation(
            "File consistency guard: Updated metadata of {NumberOfFiles} local files in {ElapsedTime}",
            processedFiles,
            Stopwatch.GetElapsedTime(startTimestamp));

        return true;
    }

    private static void SetHash(FileConsistencyGuardFileModel file, string hash, long? trailingZeroBytesLength)
    {
        file.Status = FileConsistencyGuardFileStatus.None;
        file.Reason = FileConsistencyGuardFileReason.None;
        file.Error = FileConsistencyGuardFileError.None;
        file.LocalHash = hash;
        file.TrailingZeroBytesLength = trailingZeroBytesLength;

        file.UpdateStatus();
    }

    private static void MarkDiverged(FileConsistencyGuardFileModel file)
    {
        file.Status = FileConsistencyGuardFileStatus.None;
        file.Reason = FileConsistencyGuardFileReason.None;
        file.Error = FileConsistencyGuardFileError.LocalDiverged;
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
        file.Error = FileConsistencyGuardFileError.LocalFailed;
    }

    private static void MarkSkipped(FileConsistencyGuardFileModel file, FileConsistencyGuardFileReason reason)
    {
        file.Status = FileConsistencyGuardFileStatus.Skipped;
        file.Reason = reason;
        file.Error = FileConsistencyGuardFileError.None;
    }

    private async Task<int> UpdateMetadataAsync(CancellationToken cancellationToken)
    {
        // We skip files on disabled roots during second and subsequent runs, because the root cannot become enabled without restarting synchronization.
        // Files can be moved to enabled root by the user, but that is less likely. Those files will be checked upon restarting synchronization.
        var files = (await _database.FileRepository
                .GetFilesByStatusAsync(FileConsistencyGuardFileStatus.None, includeDisabledRoots: _isFirstRun)
                .ConfigureAwait(false))
            .Where(x => x.LocalHash is null || x.TrailingZeroBytesLength is null)
            .ToList();

        if (files.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation("File consistency guard: Updating metadata of {NumberOfFiles} local files", files.Count);
        var startTime = Stopwatch.GetTimestamp();

        foreach (var filesBatch in files.Batch(BatchSize))
        {
            foreach (var file in filesBatch)
            {
                await UpdateFileAsync(file, cancellationToken).ConfigureAwait(false);

                var elapsedTime = Stopwatch.GetElapsedTime(startTime);

                if (elapsedTime >= ThrottleSamplingInterval)
                {
                    var delayDuration = elapsedTime * ThrottleDelayCoefficient;
                    if (delayDuration > MaxThrottleDelay)
                    {
                        delayDuration = MaxThrottleDelay;
                    }

                    // Delay to reduce CPU and I/O pressure
                    await Task.Delay(delayDuration, cancellationToken).ConfigureAwait(false);
                    startTime = Stopwatch.GetTimestamp();
                }
            }

            await _database.FileRepository.UpdateFilesAsync(filesBatch).ConfigureAwait(false);
        }

        _isFirstRun = false;
        return files.Count;
    }

    private async Task UpdateFileAsync(FileConsistencyGuardFileModel file, CancellationToken cancellationToken)
    {
        var fileRevision = await OpenFileForReadingAsync(file, cancellationToken).ConfigureAwait(false);

        if (fileRevision is null)
        {
            return;
        }

        await using (fileRevision.ConfigureAwait(false))
        {
            var metadata = await HashContentAsync(fileRevision, file, cancellationToken).ConfigureAwait(false);

            if (metadata is not null)
            {
                SetHash(file, metadata.Value.Hash, metadata.Value.TrailingZeroBytesLength);
            }
        }
    }

    private async Task<ISourceRevision?> OpenFileForReadingAsync(FileConsistencyGuardFileModel file, CancellationToken cancellationToken)
    {
        try
        {
            var fileRevision = await _fileRevisionProvider.OpenFileForReadingAsync(file.Id, file.ContentVersion, cancellationToken).ConfigureAwait(false);

            if (fileRevision.CreationTimeUtc >= _notApplicableIfCreatedAfter)
            {
                MarkSkipped(file, FileConsistencyGuardFileReason.CreationTime);
                return null;
            }

            return fileRevision;
        }
        catch (FileRevisionProviderException ex)
        {
            HandleOpeningFileForReadingException(ex, file);
            return null;
        }
    }

    private async Task<(string Hash, long TrailingZeroBytesLength)?> HashContentAsync(ISourceRevision? revision, FileConsistencyGuardFileModel file, CancellationToken cancellationToken)
    {
        if (revision is null)
        {
            return null;
        }

        try
        {
            var trailingZeroBytesCountingStream = new TrailingZeroBytesCountingStream(revision.GetContentStream());

            var bufferedStream = new BufferedStream(trailingZeroBytesCountingStream, bufferSize: 64 * 1024);

            await using (bufferedStream.ConfigureAwait(false))
            {
                var hash = await SHA1.HashDataAsync(bufferedStream, cancellationToken).ConfigureAwait(false);

                if (revision.TryGetFileHasChanged(out var hasChanged) && hasChanged)
                {
                    throw new FileSystemClientException<long>(
                        "File transfer aborted. File has changed before the transfer was completed",
                        FileSystemErrorCode.TransferAbortedDueToFileChange,
                        file.Id);
                }

                return (Hash: Convert.ToHexStringLower(hash), trailingZeroBytesCountingStream.TrailingZeroBytesLength);
            }
        }
        catch (FileSystemClientException ex)
        {
            _logger.LogWarning("File consistency guard: Hashing file with Id={Id} failed: {ErrorMessage}", file.Id, ex.CombinedMessage());

            if (ex.ErrorCode is FileSystemErrorCode.TransferAbortedDueToFileChange)
            {
                MarkDiverged(file);
            }
            else
            {
                MarkFailed(file);
            }

            _errorCounter.Add(ErrorScope.DataIntegrityItemOperation, ex);
            return null;
        }
    }

    private void HandleOpeningFileForReadingException(FileRevisionProviderException ex, FileConsistencyGuardFileModel file)
    {
        if (ex.ProviderErrorCode is FileRevisionProviderErrorCode.RootDisabled)
        {
            MarkRootDisabled(file);
        }
        else if (ex.ProviderErrorCode is FileRevisionProviderErrorCode.NotAFile
            || ex.ErrorCode is FileSystemErrorCode.ObjectNotFound)
        {
            MarkSkipped(file, FileConsistencyGuardFileReason.Deleted);
        }
        else if (ex.ProviderErrorCode is FileRevisionProviderErrorCode.ContentVersionDiverged)
        {
            MarkSkipped(file, FileConsistencyGuardFileReason.Updated);
        }
        else if (ex.ErrorCode is
                 FileSystemErrorCode.DirectoryNotFound or
                 FileSystemErrorCode.PathNotFound or
                 FileSystemErrorCode.IdentityMismatch or
                 FileSystemErrorCode.MetadataMismatch)
        {
            _logger.LogWarning("File consistency guard: Opening file with Id={Id} failed: {ErrorMessage}", file.Id, ex.CombinedMessage());
            MarkDiverged(file);
            _errorCounter.Add(ErrorScope.DataIntegrityItemOperation, ex);
        }
        else if (ex.ErrorCode is FileSystemErrorCode.Partial)
        {
            SetHash(file, PartialFileIndicator, trailingZeroBytesLength: null);
        }
        else
        {
            _logger.LogWarning("File consistency guard: Opening file with Id={Id} failed: {ErrorMessage}", file.Id, ex.CombinedMessage());
            MarkFailed(file);
            _errorCounter.Add(ErrorScope.DataIntegrityItemOperation, ex);
        }
    }
}
