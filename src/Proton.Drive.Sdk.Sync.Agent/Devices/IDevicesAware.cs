namespace Proton.Drive.Sdk.Sync.Agent.Devices;

public interface IDevicesAware
{
    void OnDeviceChanged(DeviceChangeType changeType, Device device);
}
