using ProtonDrive.App.Volumes;

namespace ProtonDrive.App.Photos.Volume;

public interface IPhotoVolumeStateAware
{
    void OnPhotoVolumeStateChanged(VolumeState value);
}
