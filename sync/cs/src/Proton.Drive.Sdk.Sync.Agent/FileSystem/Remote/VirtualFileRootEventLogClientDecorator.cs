using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Remote;

/// <summary>
/// Filters events for the single file that is individually shared with me.
/// The file is presented as existing in a virtual root folder.
/// </summary>
internal sealed class VirtualFileRootEventLogClientDecorator : IEventLogClient<string>
{
    private readonly string _fileId;
    private readonly string _fileName;
    private readonly string _virtualFolderId;
    private readonly IEventLogClient<string> _decoratedInstance;

    public VirtualFileRootEventLogClientDecorator(
        string fileId,
        string fileName,
        string virtualFolderId,
        IEventLogClient<string> instanceToDecorate)
    {
        _fileId = fileId;
        _fileName = fileName;
        _virtualFolderId = virtualFolderId;
        _decoratedInstance = instanceToDecorate;

        _decoratedInstance.LogEntriesReceived += OnDecoratedInstanceLogEntriesReceived;
    }

    public event EventHandler<EventLogEntriesReceivedEventArgs<string>>? LogEntriesReceived;

    public void Enable() => _decoratedInstance.Enable();

    public void Disable() => _decoratedInstance.Disable();

    public Task GetEventsAsync() => _decoratedInstance.GetEventsAsync();

    private void OnDecoratedInstanceLogEntriesReceived(object? sender, EventLogEntriesReceivedEventArgs<string> eventArgs)
    {
        var transformedEntries =
            eventArgs.Entries
                .Where(IsEntryRelevant)
                .Select(ToTransformedEntry)
                .ToList()
                .AsReadOnly();

        var rootedEventArgs = new EventLogEntriesReceivedEventArgs<string>(transformedEntries, eventArgs.ConsiderEventsProcessed)
        {
            VolumeId = eventArgs.VolumeId,
            Scope = eventArgs.Scope,
        };

        LogEntriesReceived?.Invoke(this, rootedEventArgs);
    }

    private bool IsEntryRelevant(EventLogEntry<string> entry)
    {
        return
            (_fileId.Equals(entry.Id) && entry.ChangeType is EventLogChangeType.Changed or EventLogChangeType.ChangedOrMoved)
            || entry.ChangeType is EventLogChangeType.Skipped or EventLogChangeType.Error;
    }

    private EventLogEntry<string> ToTransformedEntry(EventLogEntry<string> entry)
    {
        if (entry.ChangeType is EventLogChangeType.Skipped or EventLogChangeType.Error)
        {
            return entry;
        }

        var changeType = entry.ChangeType switch
        {
            EventLogChangeType.Changed => EventLogChangeType.Changed,
            EventLogChangeType.ChangedOrMoved => EventLogChangeType.Changed,
            _ => throw new InvalidOperationException($"Event log change type has unexpected value {entry.ChangeType}"),
        };

        return entry with
        {
            ChangeType = changeType,
            Name = _fileName,
            ParentId = _virtualFolderId,
        };
    }
}
