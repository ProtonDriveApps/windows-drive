namespace Proton.Drive.App.Photos.Volume;

internal interface IPhotoVolumeService
{
    Task RetryFailedSetupAsync();
}
