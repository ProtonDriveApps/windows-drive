namespace ProtonDrive.App.Devices;

public interface IDeviceServiceStateAware
{
    void OnDeviceServiceStateChanged(DeviceServiceStatus status);
}
