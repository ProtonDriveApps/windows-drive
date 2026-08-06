using Proton.Drive.Sdk.Sync.Agent.Mapping.SyncFolders;

namespace Proton.Drive.App.Photos.Import;

public interface IPhotoImportFoldersAware
{
    void OnPhotoImportFolderChanged(SyncFolderChangeType changeType, PhotoImportFolderState folder);
}
