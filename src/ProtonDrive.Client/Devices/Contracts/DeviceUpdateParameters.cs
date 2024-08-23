namespace ProtonDrive.Client.Devices.Contracts;

internal sealed class DeviceUpdateParameters
{
    private DeviceDeviceUpdateParameters? _device;
    private DeviceShareUpdateParameters? _share;

    public DeviceDeviceUpdateParameters Device
    {
        get => _device ??= new();
        init => _device = value;
    }

    public DeviceShareUpdateParameters Share
    {
        get => _share ??= new();
        init => _share = value;
    }
}
