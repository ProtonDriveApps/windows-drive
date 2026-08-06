using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.DataAccess.Databases;
using Proton.Drive.Sdk.Sync.Shared.Health;

namespace Proton.Drive.Sdk.Sync.Agent.Health.FileConsistency;

internal sealed class FileConsistencyGuardFileSanitizer
{
    private static readonly TimeSpan DelayBetweenFiles = TimeSpan.FromMilliseconds(50);

    private readonly FileConsistencyGuardDatabase _database;
    private readonly LocalFileMetadataValidator _localFileMetadataValidator;
    private readonly RemoteFileDownloadTrigger _remoteFileDownloadTrigger;
    private readonly ILogger<FileConsistencyGuardFileSanitizer> _logger;

    private bool _isFirstRun = true;

    public FileConsistencyGuardFileSanitizer(
        FileConsistencyGuardDatabase database,
        LocalFileMetadataValidator localFileMetadataValidator,
        RemoteFileDownloadTrigger remoteFileDownloadTrigger,
        ILogger<FileConsistencyGuardFileSanitizer> logger)
    {
        _database = database;
        _localFileMetadataValidator = localFileMetadataValidator;
        _remoteFileDownloadTrigger = remoteFileDownloadTrigger;
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        var startTimestamp = Stopwatch.GetTimestamp();

        var processedFiles = await SanitizeFilesAsync(cancellationToken).ConfigureAwait(false);

        if (processedFiles == 0)
        {
            _logger.LogDebug("File consistency guard: No files require sanitization");
            return false;
        }

        _logger.LogInformation(
            "File consistency guard: Sanitized {NumberOfFiles} files in {ElapsedTime}",
            processedFiles,
            Stopwatch.GetElapsedTime(startTimestamp));

        return true;
    }

    private static void MarkSanitized(FileConsistencyGuardFileModel file)
    {
        file.Status = FileConsistencyGuardFileStatus.Sanitized;
        file.Error = FileConsistencyGuardFileError.None;
    }

    private async Task<int> SanitizeFilesAsync(CancellationToken cancellationToken)
    {
        var files = (await _database.FileRepository.GetFilesByStatusAsync(
                FileConsistencyGuardFileStatus.Inconsistent,
                includeDisabledRoots: _isFirstRun).ConfigureAwait(false))
            .ToList();

        if (files.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation("File consistency guard: Sanitizing {NumberOfFiles} files", files.Count);

        var succeededCount = 0;
        var failedCount = 0;

        foreach (var file in files)
        {
            await SanitizeFileAsync(file, cancellationToken).ConfigureAwait(false);

            await UpdateFileAsync(file).ConfigureAwait(false);

            if (file.Error is FileConsistencyGuardFileError.None)
            {
                succeededCount++;
            }
            else
            {
                failedCount++;
            }

            await Task.Delay(DelayBetweenFiles, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "File consistency guard: Sanitization completed ({Succeeded} succeeded, {Failed} failed)",
            succeededCount,
            failedCount);

        _isFirstRun = false;
        return files.Count;
    }

    private async Task SanitizeFileAsync(FileConsistencyGuardFileModel file, CancellationToken cancellationToken)
    {
        if (!await ValidateLocalFileAsync(file, cancellationToken).ConfigureAwait(false) ||
            !await TriggerFileDownloadAsync(file, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        MarkSanitized(file);
    }

    private Task<bool> ValidateLocalFileAsync(FileConsistencyGuardFileModel file, CancellationToken cancellationToken)
    {
        return _localFileMetadataValidator.ExecuteAsync(file, cancellationToken);
    }

    private Task<bool> TriggerFileDownloadAsync(FileConsistencyGuardFileModel file, CancellationToken cancellationToken)
    {
        return _remoteFileDownloadTrigger.ExecuteAsync(file, cancellationToken);
    }

    private Task UpdateFileAsync(FileConsistencyGuardFileModel file)
    {
        return _database.FileRepository.UpdateFilesAsync([file]);
    }
}
