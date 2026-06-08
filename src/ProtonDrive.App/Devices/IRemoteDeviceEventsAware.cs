namespace ProtonDrive.App.Devices;

internal interface IRemoteDeviceEventsAware
{
    void OnDeviceCreated(string deviceLinkId);

    void OnDeviceDeleted(string deviceLinkId);
}
