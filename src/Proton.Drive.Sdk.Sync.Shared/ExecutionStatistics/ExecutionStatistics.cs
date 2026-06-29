namespace Proton.Drive.Sdk.Sync.Shared.ExecutionStatistics;

public record ExecutionStatistics : IExecutionStatistics
{
    public int Succeeded { get; init; }
    public int Failed { get; init; }
    public int Skipped { get; init; }
}
