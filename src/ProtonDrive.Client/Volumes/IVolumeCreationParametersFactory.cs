using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Contracts;

namespace ProtonDrive.Client.Volumes;

internal interface IVolumeCreationParametersFactory
{
    Task<VolumeCreationParameters> CreateAsync(CancellationToken cancellationToken);
}
