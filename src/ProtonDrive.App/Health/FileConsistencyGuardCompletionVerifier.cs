using Microsoft.Extensions.Logging;
using ProtonDrive.Sync.Shared.Health;

namespace ProtonDrive.App.Health;

internal sealed class FileConsistencyGuardCompletionVerifier
{
    private readonly ILogger<FileConsistencyGuardCompletionVerifier> _logger;

    public FileConsistencyGuardCompletionVerifier(ILogger<FileConsistencyGuardCompletionVerifier> logger)
    {
        _logger = logger;
    }

    public CompletionVerdict Execute(IReadOnlyCollection<FileConsistencyGuardFileStatisticsEntry> statistics)
    {
        _logger.LogDebug("File consistency guard: Verifying completion");

        var hasCompleted = HasCompleted(statistics);

        if (hasCompleted)
        {
            _logger.LogInformation("File consistency guard: Completed");
        }
        else
        {
            _logger.LogDebug("File consistency guard: Not yet completed");
        }

        return hasCompleted ? CompletionVerdict.Completed : CompletionVerdict.NotCompleted;
    }

    private static bool HasCompleted(IReadOnlyCollection<FileConsistencyGuardFileStatisticsEntry> statistics)
    {
        var containsFilesToVerify = statistics.Any(x => x.Status is FileConsistencyGuardFileStatus.None);
        var containsFilesToSanitize = statistics.Any(x => x.Status is FileConsistencyGuardFileStatus.Inconsistent);
        var containsFilesToDownload = statistics.Any(x => x.Status is FileConsistencyGuardFileStatus.Skipped
            && x.Reason
                is FileConsistencyGuardFileReason.SizeUnknown
                or FileConsistencyGuardFileReason.ChecksumUnknown
                or FileConsistencyGuardFileReason.SizeRule
                or FileConsistencyGuardFileReason.LastBytes);

        return !containsFilesToVerify
            && !containsFilesToSanitize
            && !containsFilesToDownload;
    }
}
