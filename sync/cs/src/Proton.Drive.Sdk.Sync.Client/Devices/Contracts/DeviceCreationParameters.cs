using Proton.Drive.Sdk.Sync.Client.Shares.Contracts;

namespace Proton.Drive.Sdk.Sync.Client.Devices.Contracts;

internal sealed class DeviceCreationParameters
{
    public required DeviceDeviceCreationParameters Device { get; init; }
    public required ShareCreationParameters Share { get; init; }
    public required LinkCreationParameters Link { get; init; }
}
