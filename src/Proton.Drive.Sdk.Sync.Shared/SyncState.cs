using Proton.Drive.Sdk.Sync.Shared.SyncActivity;

namespace Proton.Drive.Sdk.Sync.Shared;

public record SyncState
{
    internal SyncState(SyncStatus status, bool failed, DateTimeOffset? pausedUntil = null)
    {
        Status = status;
        Failed = failed;
        PausedUntil = pausedUntil;
    }

    public static SyncState Initializing { get; } = new(SyncStatus.Initializing, false);
    public static SyncState Synchronizing { get; } = new(SyncStatus.Synchronizing, false);
    public static SyncState Idle { get; } = new(SyncStatus.Idle, false);
    public static SyncState Terminated { get; } = new(SyncStatus.Terminated, false);

    public SyncStatus Status { get; }
    public bool Failed { get; }
    public DateTimeOffset? PausedUntil { get; }
}
