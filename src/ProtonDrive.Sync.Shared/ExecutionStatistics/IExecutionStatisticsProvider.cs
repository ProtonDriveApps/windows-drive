namespace ProtonDrive.Sync.Shared.ExecutionStatistics;

public interface IExecutionStatisticsProvider
{
    IExecutionStatistics ExecutionStatistics { get; }
}
