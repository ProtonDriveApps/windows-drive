using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.DataAccess.Databases;
using Proton.Drive.Sdk.Sync.Shared.Adapters;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Health;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.IO;
using Proton.Drive.Shared.Telemetry;

namespace Proton.Drive.Sdk.Sync.Agent.Health.FileConsistency;

internal sealed class RemoteFileMetadataCalculator
{
    private const double ThrottleDelayCoefficient = 0.2;
    private static readonly TimeSpan ThrottleSamplingInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxThrottleDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DelayBetweenFiles = TimeSpan.FromMilliseconds(100);

    private readonly FileConsistencyGuardDatabase _database;
    private readonly IFileRevisionProvider<long> _fileRevisionProvider;
    private readonly IErrorCounter _errorCounter;
    private readonly ILogger<RemoteFileMetadataCalculator> _logger;

    private bool _isWave1FirstRun = true;
    private bool _isWave2FirstRun = true;

    public RemoteFileMetadataCalculator(
        FileConsistencyGuardDatabase database,
        IFileRevisionProvider<long> fileRevisionProvider,
        IErrorCounter errorCounter,
        ILogger<RemoteFileMetadataCalculator> logger)
    {
        _database = database;
        _fileRevisionProvider = fileRevisionProvider;
        _errorCounter = errorCounter;
        _logger = logger;
    }

    public async Task<bool> ExecuteWave1Async(CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(GetWave1FilesAsync, "Wave 1", cancellationToken).ConfigureAwait(false);

        if (result)
        {
            _isWave1FirstRun = false;
        }

        return result;
    }

    public async Task<bool> ExecuteWave2Async(CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(GetWave2FilesAsync, "Wave 2", cancellationToken).ConfigureAwait(false);

        if (result)
        {
            _isWave2FirstRun = false;
        }

        return result;
    }

    private async Task<bool> ExecuteAsync(Func<Task<IReadOnlyCollection<FileConsistencyGuardFileModel>>> getFiles, string wave, CancellationToken cancellationToken)
    {
        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            var files = await getFiles.Invoke().ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            var processedFiles = await CalculateMetadataAsync(files, wave, cancellationToken).ConfigureAwait(false);

            if (processedFiles == 0)
            {
                _logger.LogDebug("File consistency guard: No remote files require metadata calculation ({Wave})", wave);
                return false;
            }

            _logger.LogInformation(
                "File consistency guard: Calculated metadata of {NumberOfFiles} remote files in {ElapsedTime} ({Wave})",
                processedFiles,
                Stopwatch.GetElapsedTime(startTimestamp),
                wave);

            return true;
        }
        catch (FileSystemClientException ex) when (ex.ErrorCode is FileSystemErrorCode.Offline)
        {
            // Skip processing when offline
            return true;
        }
    }

    private static void SetMetadata(FileConsistencyGuardFileModel file, string hash, long plainSize)
    {
        file.DownloadReason = file.Reason;
        file.Error = FileConsistencyGuardFileError.None;
        file.RemoteHash = hash;
        file.RemotePlainSize = plainSize;

        file.UpdateStatus();
    }

    private static void MarkDiverged(FileConsistencyGuardFileModel file)
    {
        file.Error = FileConsistencyGuardFileError.RemoteDiverged;
    }

    private static void MarkRootDisabled(FileConsistencyGuardFileModel file)
    {
        file.Error = FileConsistencyGuardFileError.RootDisabled;
    }

    private static void MarkFailed(FileConsistencyGuardFileModel file)
    {
        file.Error = FileConsistencyGuardFileError.RemoteFailed;
    }

    private static void MarkSkipped(FileConsistencyGuardFileModel file, FileConsistencyGuardFileReason reason)
    {
        file.Status = FileConsistencyGuardFileStatus.Skipped;
        file.Reason = reason;
        file.Error = FileConsistencyGuardFileError.None;
    }

    private async Task<IReadOnlyCollection<FileConsistencyGuardFileModel>> GetWave1FilesAsync()
    {
        return (await _database.FileRepository
                .GetFilesByStatusAsync(FileConsistencyGuardFileStatus.Skipped, includeDisabledRoots: _isWave1FirstRun)
                .ConfigureAwait(false))
            .Where(x => x.Reason is FileConsistencyGuardFileReason.SizeUnknown or FileConsistencyGuardFileReason.ChecksumUnknown)
            .ToList();
    }

    private async Task<IReadOnlyCollection<FileConsistencyGuardFileModel>> GetWave2FilesAsync()
    {
        return (await _database.FileRepository
                .GetFilesByStatusAsync(FileConsistencyGuardFileStatus.Skipped, includeDisabledRoots: _isWave2FirstRun)
                .ConfigureAwait(false))
            .Where(x => x.Reason is FileConsistencyGuardFileReason.SizeRule or FileConsistencyGuardFileReason.LastBytes)
            .ToList();
    }

    private async Task<int> CalculateMetadataAsync(IReadOnlyCollection<FileConsistencyGuardFileModel> files, string wave, CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            return 0;
        }

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("File consistency guard: Calculating metadata of {NumberOfFiles} remote files ({Wave})", files.Count, wave);

        var (succeeded, failed) = await ProcessBatchAsync(files, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "File consistency guard: Remote files metadata calculation completed ({Succeeded} succeeded, {Failed} failed)",
            succeeded,
            failed);

        return files.Count;
    }

    private async Task<(object Succeeded, object Failed)> ProcessBatchAsync(IEnumerable<FileConsistencyGuardFileModel> files, CancellationToken cancellationToken)
    {
        var succeeded = 0;
        var failed = 0;
        var startTime = Stopwatch.GetTimestamp();

        foreach (var file in files)
        {
            await ProcessFileAsync(file, cancellationToken).ConfigureAwait(false);

            await UpdateFileAsync(file).ConfigureAwait(false);

            if (file.Error is FileConsistencyGuardFileError.None)
            {
                succeeded++;
            }
            else
            {
                failed++;
            }

            var elapsedTime = Stopwatch.GetElapsedTime(startTime);

            if (elapsedTime >= ThrottleSamplingInterval)
            {
                var delayDuration = elapsedTime * ThrottleDelayCoefficient;
                if (delayDuration > MaxThrottleDelay)
                {
                    delayDuration = MaxThrottleDelay;
                }

                // Delay to reduce pressure on the network
                await Task.Delay(delayDuration, cancellationToken).ConfigureAwait(false);
                startTime = Stopwatch.GetTimestamp();
            }

            await Task.Delay(DelayBetweenFiles, cancellationToken).ConfigureAwait(false);
        }

        return (succeeded, failed);
    }

    private async Task ProcessFileAsync(FileConsistencyGuardFileModel file, CancellationToken cancellationToken)
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
                SetMetadata(file, metadata.Value.Hash, metadata.Value.PlainSize);
            }
        }
    }

    private async Task<ISourceRevision?> OpenFileForReadingAsync(FileConsistencyGuardFileModel file, CancellationToken cancellationToken)
    {
        try
        {
            return await _fileRevisionProvider.OpenFileForReadingAsync(file.Id, file.ContentVersion, cancellationToken).ConfigureAwait(false);
        }
        catch (FileRevisionProviderException ex)
        {
            HandleOpeningFileForReadingException(ex, file);
            return null;
        }
    }

    private async Task<(string Hash, long PlainSize)?> HashContentAsync(ISourceRevision? fileRevision, FileConsistencyGuardFileModel file, CancellationToken cancellationToken)
    {
        if (fileRevision is null)
        {
            return null;
        }

        try
        {
            await using (fileRevision.ConfigureAwait(false))
            {
                var hashingStream = new HashingStream(Stream.Null, HashAlgorithmName.SHA1);

                await using (hashingStream.ConfigureAwait(false))
                {
                    await fileRevision.CopyContentToAsync(hashingStream, cancellationToken).ConfigureAwait(false);

                    Span<byte> hashBytes = stackalloc byte[SHA1.HashSizeInBytes];
                    hashingStream.GetCurrentHash(hashBytes);

                    return (Hash: Convert.ToHexStringLower(hashBytes), PlainSize: hashingStream.NumberOfBytesHashed);
                }
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
        else if (ex.ErrorCode is FileSystemErrorCode.MetadataMismatch)
        {
            _logger.LogWarning("File consistency guard: Opening file with Id={Id} failed: {ErrorMessage}", file.Id, ex.CombinedMessage());
            MarkDiverged(file);
            _errorCounter.Add(ErrorScope.DataIntegrityItemOperation, ex);
        }
        else
        {
            _logger.LogWarning("File consistency guard: Opening file with Id={Id} failed: {ErrorMessage}", file.Id, ex.CombinedMessage());
            MarkFailed(file);
            _errorCounter.Add(ErrorScope.DataIntegrityItemOperation, ex);
        }
    }

    private Task UpdateFileAsync(FileConsistencyGuardFileModel file)
    {
        return _database.FileRepository.UpdateFilesAsync([file]);
    }
}
