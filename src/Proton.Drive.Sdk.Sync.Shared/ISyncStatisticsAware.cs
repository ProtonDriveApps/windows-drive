using Proton.Drive.Sdk.Sync.Shared.ExecutionStatistics;

namespace Proton.Drive.Sdk.Sync.Shared;

public interface ISyncStatisticsAware
{
    void OnSyncStatisticsChanged(IExecutionStatistics value);
}
