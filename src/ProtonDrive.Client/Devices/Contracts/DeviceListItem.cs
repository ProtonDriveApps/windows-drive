namespace ProtonDrive.Client.Devices.Contracts;

internal sealed record DeviceListItem
{
    private Device? _device;
    private DeviceShare? _share;

    public Device Device
    {
        get => _device ??= new();
        init => _device = value;
    }

    public DeviceShare Share
    {
        get => _share ??= new();
        init => _share = value;
    }
}
