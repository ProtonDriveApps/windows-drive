using ProtonDrive.Sync.Shared.SyncActivity;

namespace ProtonDrive.App.Sync;

public record SyncState
{
    internal SyncState(SyncStatus status, bool failed)
    {
        Status = status;
        Failed = failed;
    }

    public static SyncState Terminated { get; } = new(SyncStatus.Terminated, false);

    public SyncStatus Status { get; }
    public bool Failed { get; }
}
