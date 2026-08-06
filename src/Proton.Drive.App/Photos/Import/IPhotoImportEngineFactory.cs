using Proton.Drive.Sdk.Sync.Agent.Volumes;

namespace Proton.Drive.App.Photos.Import;

internal interface IPhotoImportEngineFactory
{
    IPhotoImportEngine CreateEngine(PhotoImportFolderState folder, VolumeInfo photoVolume);
}
