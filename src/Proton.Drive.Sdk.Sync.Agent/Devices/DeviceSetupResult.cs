using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Agent.Devices;

public sealed record DeviceSetupResult(Device? Device, ResponseCode? ErrorCode)
{
    public DeviceSetupResult(Device device)
    : this(device, ErrorCode: null)
    {
    }

    public static DeviceSetupResult Failure => new(Device: null, ErrorCode: ResponseCode.Unknown);
    public static DeviceSetupResult DeviceNotFound => new(Device: null, ErrorCode: ResponseCode.DoesNotExist);
}
