using ProtonDrive.App.Volumes;

namespace ProtonDrive.App.Photos.Import;

internal interface IPhotoImportEngineFactory
{
    IPhotoImportEngine CreateEngine(PhotoImportFolderState folder, VolumeInfo photoVolume);
}
