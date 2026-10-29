namespace Proton.Drive.Sdk.Sync.Shared;

public interface ISyncAgentStateAware
{
    public void OnSyncAgentStateChanged(SyncAgentStatus status);
}
