namespace Proton.Drive.Sdk.Sync.Shared;

public sealed record BulkDeletionState(BulkDeletionStatus Status, IReadOnlyCollection<BulkDeletionRoot> Roots)
{
    public static BulkDeletionState Empty { get; } = new(BulkDeletionStatus.None, Roots: []);
}
