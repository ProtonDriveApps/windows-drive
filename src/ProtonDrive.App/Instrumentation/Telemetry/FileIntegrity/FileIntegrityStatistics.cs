using ProtonDrive.App.Health;
using ProtonDrive.App.Services;
using ProtonDrive.Shared.Repository;
using ProtonDrive.Sync.Shared.Health;

namespace ProtonDrive.App.Instrumentation.Telemetry.FileIntegrity;

internal sealed class FileIntegrityStatistics(IRepository<FileConsistencyGuardSettings> settings) : IStartableService
{
    public FileConsistencyGuardStatus? GetOverallStatus()
    {
        return GetSettings()?.Status;
    }

    public IReadOnlyCollection<FileConsistencyGuardFileStatisticsEntry> GetDetails()
    {
        return GetSettings()?.FileStatistics ?? [];
    }

    Task IStartableService.StartAsync(CancellationToken cancellationToken)
    {
        // File consistency guard starts and reads settings file later, after the synchronization starts.
        // Reading settings now makes them cached in the memory, which avoids reading the file concurrently
        // with the file consistency guard.
        GetSettings();

        return Task.CompletedTask;
    }

    private FileConsistencyGuardSettings? GetSettings()
    {
        return settings.Get();
    }
}
