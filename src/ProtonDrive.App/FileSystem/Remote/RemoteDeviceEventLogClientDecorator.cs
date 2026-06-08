using ProtonDrive.App.Devices;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem.Remote;

internal sealed class RemoteDeviceEventLogClientDecorator : IEventLogClient<string>
{
    private readonly IRemoteDeviceEventsAware _listener;
    private readonly IEventLogClient<string> _decoratedInstance;

    public RemoteDeviceEventLogClientDecorator(
        IRemoteDeviceEventsAware listener,
        IEventLogClient<string> instanceToDecorate)
    {
        _listener = listener;
        _decoratedInstance = instanceToDecorate;
        _decoratedInstance.LogEntriesReceived += OnDecoratedInstanceLogEntriesReceived;
    }

    public event EventHandler<EventLogEntriesReceivedEventArgs<string>>? LogEntriesReceived;

    public void Enable()
    {
        _decoratedInstance.Enable();
    }

    public void Disable()
    {
        _decoratedInstance.Disable();
    }

    public Task GetEventsAsync()
    {
        return _decoratedInstance.GetEventsAsync();
    }

    private void OnDecoratedInstanceLogEntriesReceived(object? sender, EventLogEntriesReceivedEventArgs<string> e)
    {
        foreach (var entry in e.Entries)
        {
            var isPotentiallyDeviceEvent = entry.ParentId is null;

            if (!isPotentiallyDeviceEvent || entry.Id is not { } deviceLinkId)
            {
                continue;
            }

            switch (entry.ChangeType)
            {
                case EventLogChangeType.Deleted or EventLogChangeType.DeletedOrMovedFrom:
                    _listener.OnDeviceDeleted(deviceLinkId);
                    break;

                case EventLogChangeType.Created or EventLogChangeType.CreatedOrMovedTo:
                    _listener.OnDeviceCreated(deviceLinkId);
                    break;
            }
        }

        LogEntriesReceived?.Invoke(this, e);
    }
}
