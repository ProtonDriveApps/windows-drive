namespace Proton.Drive.Sdk.Sync.Shared.ExecutionStatistics;

public interface IExecutionStatisticsProvider
{
    IExecutionStatistics ExecutionStatistics { get; }
}
