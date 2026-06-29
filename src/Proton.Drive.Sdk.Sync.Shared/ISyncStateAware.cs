namespace Proton.Drive.Sdk.Sync.Shared;

public interface ISyncStateAware
{
    void OnSyncStateChanged(SyncState value);
}
