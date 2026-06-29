namespace Proton.Drive.Sdk.Sync.Shared.ExecutionStatistics;

public static class ExecutionStatisticsExtensions
{
    public static IExecutionStatistics ClearFailures(this IExecutionStatistics x)
    {
        return new ExecutionStatistics
        {
            Succeeded = x.Succeeded,
            Failed = 0,
        };
    }
}
