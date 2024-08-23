namespace ProtonDrive.App.Mapping.SyncFolders;

public interface ISyncFoldersAware
{
    void OnSyncFolderChanged(SyncFolderChangeType changeType, SyncFolder folder);
}
