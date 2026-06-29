namespace Proton.Drive.Sdk.Sync.Agent.Volumes;

public interface IMainVolumeStateAware
{
    void OnMainVolumeStateChanged(VolumeState value);
}
