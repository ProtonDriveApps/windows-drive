namespace ProtonDrive.App.Photos.Volume;

internal interface IPhotoVolumeService
{
    Task RetryFailedSetupAsync();
}
