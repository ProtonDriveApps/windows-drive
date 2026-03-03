using Microsoft.Extensions.Logging;
using ProtonDrive.DataAccess.Databases;
using ProtonDrive.Sync.Shared.Health;

namespace ProtonDrive.App.Health;

internal sealed class FileConsistencyGuardCompletionVerifier
{
    private readonly FileConsistencyGuardDatabase _database;
    private readonly ILogger<FileConsistencyGuardCompletionVerifier> _logger;

    public FileConsistencyGuardCompletionVerifier(
        FileConsistencyGuardDatabase database,
        ILogger<FileConsistencyGuardCompletionVerifier> logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task<CompletionVerdict> ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("File consistency guard: Verifying completion");

        var verdict = await VerifyCompletionAsync(cancellationToken).ConfigureAwait(false);

        if (verdict is CompletionVerdict.Completed)
        {
            _logger.LogInformation("File consistency guard: Completed");
        }
        else
        {
            _logger.LogDebug("File consistency guard: Not yet completed");
        }

        return verdict;
    }

    private async Task<CompletionVerdict> VerifyCompletionAsync(CancellationToken cancellationToken)
    {
        if (await HasInconsistentFilesAsync().ConfigureAwait(false))
        {
            return CompletionVerdict.NotCompleted;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (await HasSkippedFilesAsync().ConfigureAwait(false))
        {
            return CompletionVerdict.NotCompleted;
        }

        return CompletionVerdict.Completed;
    }

    private async Task<bool> HasInconsistentFilesAsync()
    {
        return (await _database.FileRepository
                .GetFilesByStatusAsync(FileConsistencyGuardFileStatus.Inconsistent, includeDisabledRoots: true)
                .ConfigureAwait(false))
            .Any();
    }

    private async Task<bool> HasSkippedFilesAsync()
    {
        return (await _database.FileRepository
                .GetFilesByStatusAsync(FileConsistencyGuardFileStatus.Skipped, includeDisabledRoots: true)
                .ConfigureAwait(false))
            .Any(x => x.Reason
                is FileConsistencyGuardFileReason.SizeUnknown
                or FileConsistencyGuardFileReason.ChecksumUnknown
                or FileConsistencyGuardFileReason.SizeRule
                or FileConsistencyGuardFileReason.LastBytes);
    }
}
