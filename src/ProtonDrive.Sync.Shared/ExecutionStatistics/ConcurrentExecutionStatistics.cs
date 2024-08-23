namespace ProtonDrive.Sync.Shared.ExecutionStatistics;

public class ConcurrentExecutionStatistics : IExecutionStatistics
{
    public ConcurrentInteger Succeeded { get; } = new();
    public ConcurrentInteger Failed { get; } = new();
    public ConcurrentInteger Skipped { get; } = new();

    int IExecutionStatistics.Succeeded => Succeeded.Value;
    int IExecutionStatistics.Failed => Failed.Value;
    int IExecutionStatistics.Skipped => Skipped.Value;

    public void ClearFailures()
    {
        Failed.Set(0);
        Skipped.Set(0);
    }
}
