using System;
using System.Linq;
using System.Threading.Tasks;
using ProtonDrive.Sync.Adapter;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem.Local;

internal sealed class FileTransferAbortingEventLogClientDecorator : IEventLogClient<long>
{
    private readonly AggregatingEventLogClient<long> _decoratedInstance;
    private readonly IFileTransferAbortionStrategy<long> _strategy;

    public FileTransferAbortingEventLogClientDecorator(AggregatingEventLogClient<long> instanceToDecorate, IFileTransferAbortionStrategy<long> strategy)
    {
        _decoratedInstance = instanceToDecorate;
        _strategy = strategy;
    }

    public event EventHandler<EventLogEntriesReceivedEventArgs<long>>? LogEntriesReceived
    {
        add => _decoratedInstance.LogEntriesReceived += value;
        remove => _decoratedInstance.LogEntriesReceived -= value;
    }

    public void Enable()
    {
        _decoratedInstance.LogEntriesReceived += OnLogEntriesReceived;

        _decoratedInstance.Enable();
    }

    public void Disable()
    {
        _decoratedInstance.Disable();

        _decoratedInstance.LogEntriesReceived -= OnLogEntriesReceived;
    }

    public Task GetEventsAsync()
    {
        return _decoratedInstance.GetEventsAsync();
    }

    private void OnLogEntriesReceived(object? sender, EventLogEntriesReceivedEventArgs<long> e)
    {
        foreach (var entry in e.Entries.Where(TransferAbortionIsRequired))
        {
            _strategy.HandleFileChanged((e.VolumeId, entry.Id));
        }
    }

    private static bool TransferAbortionIsRequired(EventLogEntry<long> entry) => entry.ChangeType is (
        EventLogChangeType.CreatedOrMovedTo
        or EventLogChangeType.Changed
        or EventLogChangeType.ChangedOrMoved
        or EventLogChangeType.Moved
        or EventLogChangeType.Deleted
        or EventLogChangeType.DeletedOrMovedFrom);
}
