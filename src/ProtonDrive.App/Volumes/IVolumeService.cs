using System.Threading.Tasks;

namespace ProtonDrive.App.Volumes;

public interface IVolumeService
{
    VolumeState State { get; }

    Task<VolumeInfo?> GetActiveVolumeAsync();
}
