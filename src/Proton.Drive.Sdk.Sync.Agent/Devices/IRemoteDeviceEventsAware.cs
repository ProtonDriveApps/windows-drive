namespace Proton.Drive.Sdk.Sync.Agent.Devices;

internal interface IRemoteDeviceEventsAware
{
    void OnDeviceCreated(string deviceLinkId);

    void OnDeviceDeleted(string deviceLinkId);
}
