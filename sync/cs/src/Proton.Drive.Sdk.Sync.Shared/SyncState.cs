using Proton.Drive.Sdk.Sync.Shared.SyncActivity;

namespace Proton.Drive.Sdk.Sync.Shared;

public sealed record SyncState
{
    internal SyncState(SyncStatus status)
    {
        Status = status;
    }

    public static SyncState Initializing { get; } = new(SyncStatus.Initializing);
    public static SyncState Synchronizing { get; } = new(SyncStatus.Synchronizing);
    public static SyncState Idle { get; } = new(SyncStatus.Idle);
    public static SyncState Terminated { get; } = new(SyncStatus.Terminated);

    public SyncStatus Status { get; }
    public bool Failed { get; internal init; }

    /// <summary>
    /// Point in time until when sync is paused.
    /// When specified, <see cref="PausedUntil"/> value represents either universal or local time.
    /// </summary>
    public DateTime? PausedUntil { get; internal init; }

    public BulkDeletionState BulkDeletion { get; internal init; } = BulkDeletionState.Empty;
}
