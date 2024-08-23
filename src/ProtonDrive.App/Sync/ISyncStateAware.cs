namespace ProtonDrive.App.Sync;

public interface ISyncStateAware
{
    void OnSyncStateChanged(SyncState value);
}
