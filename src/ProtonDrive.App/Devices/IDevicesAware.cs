namespace ProtonDrive.App.Devices;

public interface IDevicesAware
{
    void OnDeviceChanged(DeviceChangeType changeType, Device device);
}
