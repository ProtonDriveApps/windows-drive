namespace ProtonDrive.App.Volumes;

public interface IVolumeStateAware
{
    void OnVolumeStateChanged(VolumeState value);
}
