namespace Proton.Drive.Sdk.Sync.Shared.ExecutionStatistics;

public sealed record ExecutionStatistics : IExecutionStatistics
{
    public int Succeeded { get; init; }
    public int Failed { get; init; }
    public int Skipped { get; init; }
    public int Deleted { get; init; }
}
