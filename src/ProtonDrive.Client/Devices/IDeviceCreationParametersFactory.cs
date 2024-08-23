using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Devices.Contracts;

namespace ProtonDrive.Client.Devices;

internal interface IDeviceCreationParametersFactory
{
    Task<DeviceCreationParameters> CreateAsync(string volumeId, string name, CancellationToken cancellationToken);
}
