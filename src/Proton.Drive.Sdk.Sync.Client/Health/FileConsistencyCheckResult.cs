namespace Proton.Drive.Sdk.Sync.Client.Health;

public sealed class FileConsistencyCheckResult
{
    public int InspectedItemCount { get; init; }
    public int RefreshedItemCount { get; init; }
    public int FailedItemCount { get; init; }
}
