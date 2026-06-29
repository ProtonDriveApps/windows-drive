using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Devices.Contracts;

internal sealed record DeviceListResponse : ApiResponse
{
    private IReadOnlyCollection<DeviceListItem>? _devices;

    public IReadOnlyCollection<DeviceListItem> Devices
    {
        get => _devices ??= Array.Empty<DeviceListItem>();
        init => _devices = value;
    }
}
