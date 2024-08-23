namespace ProtonDrive.Sync.Shared.SyncActivity;

public enum SyncStatus
{
    Terminated,
    Initializing,
    Idle,
    Paused,
    DetectingUpdates,
    Synchronizing,
    Terminating,
    Offline,
    Failed,
}
