using Proton.Drive.Sdk.Sync.Client.Devices.Contracts;

namespace Proton.Drive.Sdk.Sync.Client.Devices;

internal interface IDeviceCreationParametersFactory
{
    Task<DeviceCreationParameters> CreateAsync(string volumeId, string name, CancellationToken cancellationToken);
}
