using ProtonDrive.Sync.Shared.ExecutionStatistics;

namespace ProtonDrive.Sync.Adapter.UpdateDetection.LogBased;

internal class LogBasedUpdateDetectionExecutionStatistics : IExecutionStatistics
{
    private bool _failed;

    public void Succeeded()
    {
        _failed = false;
    }

    public void Failed()
    {
        _failed = true;
    }

    // Success is not reported
    int IExecutionStatistics.Succeeded => 0;

    // Only failure is reported
    int IExecutionStatistics.Failed => _failed ? 1 : 0;

    // Skipped is not reported
    int IExecutionStatistics.Skipped => 0;
}
