namespace Proton.Drive.Sdk.Sync.Agent.Devices;

public interface IDeviceServiceStateAware
{
    void OnDeviceServiceStateChanged(DeviceServiceStatus status);
}
