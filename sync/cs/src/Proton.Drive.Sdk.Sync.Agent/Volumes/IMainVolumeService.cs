namespace Proton.Drive.Sdk.Sync.Agent.Volumes;

public interface IMainVolumeService
{
    VolumeState State { get; }

    Task<VolumeInfo?> GetVolumeAsync();
}
