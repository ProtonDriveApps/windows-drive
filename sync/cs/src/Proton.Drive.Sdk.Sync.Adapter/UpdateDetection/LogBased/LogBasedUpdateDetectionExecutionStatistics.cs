using Proton.Drive.Sdk.Sync.Shared.ExecutionStatistics;

namespace Proton.Drive.Sdk.Sync.Adapter.UpdateDetection.LogBased;

internal class LogBasedUpdateDetectionExecutionStatistics : IExecutionStatistics
{
    private bool _failed;

    // Success is not reported
    int IExecutionStatistics.Succeeded => 0;

    // Only failure is reported
    int IExecutionStatistics.Failed => _failed ? 1 : 0;

    int IExecutionStatistics.Skipped => 0;

    int IExecutionStatistics.Deleted => 0;

    public void Succeeded()
    {
        _failed = false;
    }

    public void Failed()
    {
        _failed = true;
    }
}
