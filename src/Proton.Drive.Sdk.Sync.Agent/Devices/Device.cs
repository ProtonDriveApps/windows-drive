using Proton.Drive.Sdk.Sync.Client.Devices;

namespace Proton.Drive.Sdk.Sync.Agent.Devices;

public sealed class Device
{
    public Device(DeviceType type, Proton.Drive.Sdk.Sync.Client.Devices.Device clientDevice)
    {
        Type = type;
        DataItem = clientDevice;
    }

    public string Id => DataItem.Id;
    public string LinkId => DataItem.LinkId;
    public string ShareId => DataItem.ShareId;
    public string VolumeId => DataItem.VolumeId;
    public DeviceType Type { get; }
    public string Name => DataItem.Name;
    public DevicePlatform Platform => DataItem.Platform;

    internal Proton.Drive.Sdk.Sync.Client.Devices.Device DataItem { get; private set; }

    internal bool Update(Proton.Drive.Sdk.Sync.Client.Devices.Device clientDevice)
    {
        if (clientDevice == DataItem)
        {
            return false;
        }

        DataItem = clientDevice;

        return true;
    }
}
