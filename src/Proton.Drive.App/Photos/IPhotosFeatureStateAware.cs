namespace Proton.Drive.App.Photos;

public interface IPhotosFeatureStateAware
{
    void OnPhotosFeatureStateChanged(PhotosFeatureState value);
}
