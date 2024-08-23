using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.App.Volumes;

internal interface IActiveVolumeService
{
    Task<VolumeInfo> GetActiveVolumeAsync(CancellationToken cancellationToken);
}
