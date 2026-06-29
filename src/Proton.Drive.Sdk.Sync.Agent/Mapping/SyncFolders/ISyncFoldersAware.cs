namespace Proton.Drive.Sdk.Sync.Agent.Mapping.SyncFolders;

public interface ISyncFoldersAware
{
    void OnSyncFolderChanged(SyncFolderChangeType changeType, SyncFolder folder);
}
