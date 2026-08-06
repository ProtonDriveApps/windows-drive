using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Devices.Contracts;

internal sealed record DeviceCreationResponse : ApiResponse
{
    private CreatedDevice? _device;

    public CreatedDevice Device
    {
        get => _device ??= new CreatedDevice();
        set => _device = value;
    }
}
