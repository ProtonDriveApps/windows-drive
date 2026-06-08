namespace ProtonDrive.App.Devices;

public interface IDeviceService
{
    Task SetUpDevicesAsync();
    Task<DeviceSetupResult> SetUpHostDeviceAsync(CancellationToken cancellationToken);
    Task<DeviceSetupResult> GetHostDeviceAsync(CancellationToken cancellationToken);
    Task RenameHostDeviceAsync(string name);
}
