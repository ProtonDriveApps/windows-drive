using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem;

internal sealed class RootDeletionDetectingEventLogClientDecorator<TId> : IEventLogClient<TId>
{
    private readonly IRootDeletionDetector<TId> _deletionDetector;
    private readonly IEventLogClient<TId> _decoratedInstance;

    public RootDeletionDetectingEventLogClientDecorator(
        IRootDeletionDetector<TId> deletionDetector,
        IEventLogClient<TId> instanceToDecorate)
    {
        _deletionDetector = deletionDetector;
        _decoratedInstance = instanceToDecorate;

        _decoratedInstance.LogEntriesReceived += OnDecoratedInstanceLogEntriesReceived;
    }

    public event EventHandler<EventLogEntriesReceivedEventArgs<TId>>? LogEntriesReceived;

    public void Disable()
    {
        _decoratedInstance.Disable();
    }

    public void Enable()
    {
        _decoratedInstance.Enable();
    }

    public Task GetEventsAsync()
    {
        return _decoratedInstance.GetEventsAsync();
    }

    private void OnDecoratedInstanceLogEntriesReceived(object? sender, EventLogEntriesReceivedEventArgs<TId> e)
    {
        _deletionDetector.HandleEventLogEntries(e.VolumeId, e.Entries);

        LogEntriesReceived?.Invoke(this, e);
    }
}
