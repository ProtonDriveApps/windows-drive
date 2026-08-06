using Proton.Drive.Sdk.Sync.Agent.Volumes;

namespace Proton.Drive.App.Photos.Volume;

public interface IPhotoVolumeStateAware
{
    void OnPhotoVolumeStateChanged(VolumeState value);
}
