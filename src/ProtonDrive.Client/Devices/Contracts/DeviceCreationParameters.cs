namespace ProtonDrive.Client.Devices.Contracts;

internal sealed class DeviceCreationParameters
{
    private DeviceDeviceCreationParameters? _device;
    private DeviceShareCreationParameters? _share;
    private DeviceLinkCreationParameters? _link;

    public DeviceDeviceCreationParameters Device
    {
        get => _device ??= new();
        init => _device = value;
    }

    public DeviceShareCreationParameters Share
    {
        get => _share ??= new();
        init => _share = value;
    }

    public DeviceLinkCreationParameters Link
    {
        get => _link ??= new();
        init => _link = value;
    }
}
