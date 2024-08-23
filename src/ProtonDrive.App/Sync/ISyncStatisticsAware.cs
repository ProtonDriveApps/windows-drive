using ProtonDrive.Sync.Shared.ExecutionStatistics;

namespace ProtonDrive.App.Sync;

public interface ISyncStatisticsAware
{
    void OnSyncStatisticsChanged(IExecutionStatistics value);
}
