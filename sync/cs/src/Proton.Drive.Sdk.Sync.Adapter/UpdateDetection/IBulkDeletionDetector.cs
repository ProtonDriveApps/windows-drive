using Proton.Drive.Sdk.Sync.Shared.ExecutionStatistics;

namespace Proton.Drive.Sdk.Sync.Adapter.UpdateDetection;

internal interface IBulkDeletionDetector
{
    void BeginDetection(IExecutionStatistics executionStatistics);
    bool ContinueDetection(IExecutionStatistics executionStatistics);
}
