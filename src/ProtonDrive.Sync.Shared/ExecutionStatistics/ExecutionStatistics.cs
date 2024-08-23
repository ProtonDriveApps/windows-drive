namespace ProtonDrive.Sync.Shared.ExecutionStatistics;

internal record ExecutionStatistics : IExecutionStatistics
{
    public int Succeeded { get; init; }
    public int Failed { get; init; }
    public int Skipped { get; init; }
}
