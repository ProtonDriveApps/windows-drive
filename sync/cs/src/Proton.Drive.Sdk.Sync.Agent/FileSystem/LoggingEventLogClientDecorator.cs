using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem;

internal sealed class LoggingEventLogClientDecorator<TId> : IEventLogClient<TId>
{
    private readonly ILogger<LoggingEventLogClientDecorator<TId>> _logger;
    private readonly int _volumeId;
    private readonly string _scope;
    private readonly IEventLogClient<TId> _decoratedInstance;

    public LoggingEventLogClientDecorator(
        ILogger<LoggingEventLogClientDecorator<TId>> logger,
        int volumeId,
        string scope,
        IEventLogClient<TId> instanceToDecorate)
    {
        _logger = logger;
        _volumeId = volumeId;
        _scope = scope;
        _decoratedInstance = instanceToDecorate;

        _decoratedInstance.LogEntriesReceived += OnDecoratedInstanceLogEntriesReceived;
    }

    public event EventHandler<EventLogEntriesReceivedEventArgs<TId>>? LogEntriesReceived;

    public void Enable()
    {
        _logger.LogInformation("Enabling directory change observation on {Volume}/\"{Scope}\"", _volumeId, _scope);

        _decoratedInstance.Enable();
    }

    public void Disable()
    {
        _logger.LogInformation("Disabling directory change observation on {Volume}/\"{Scope}\"", _volumeId, _scope);

        _decoratedInstance.Disable();
    }

    public Task GetEventsAsync() => _decoratedInstance.GetEventsAsync();

    private static string ToType(FileAttributes attributes)
    {
        return attributes.HasFlag(FileAttributes.Directory)
            ? "Directory"
            : "File";
    }

    private void OnDecoratedInstanceLogEntriesReceived(object? sender, EventLogEntriesReceivedEventArgs<TId> e)
    {
        foreach (var entry in e.Entries)
        {
            switch (entry.ChangeType)
            {
                case EventLogChangeType.Created:
                case EventLogChangeType.CreatedOrMovedTo:
                case EventLogChangeType.Changed:
                case EventLogChangeType.ChangedOrMoved:
                case EventLogChangeType.Deleted:
                case EventLogChangeType.DeletedOrMovedFrom:
                    LogEntry(entry);
                    break;
                case EventLogChangeType.Moved:
                    LogMove(entry);
                    break;
                case EventLogChangeType.Skipped:
                case EventLogChangeType.Error:
                    LogError(entry);
                    break;
                default:
                    throw new InvalidEnumArgumentException();
            }
        }

        LogEntriesReceived?.Invoke(this, e);
    }

    private void LogEntry(EventLogEntry<TId> entry)
    {
        _logger.LogDebug(
            "Event received on {Volume}/\"{Scope}\": {changeType} {Type} \"{path}\"/{ParentId}/{Id}, Attributes=({Attributes}), PlaceholderState=({PlaceholderState}), LastWriteTime={LastWriteTime:O}, Size={Size}, Revision={RevisionId}",
            _volumeId,
            _scope,
            entry.ChangeType,
            ToType(entry.Attributes),
            entry.Path,
            entry.ParentId,
            entry.Id,
            entry.Attributes,
            entry.PlaceholderState,
            entry.LastWriteTimeUtc,
            entry.Size ?? entry.SizeOnStorage,
            entry.RevisionId);
    }

    private void LogMove(EventLogEntry<TId> entry)
    {
        _logger.LogDebug(
            "Event received on {Volume}/\"{Scope}\": {changeType} {Type} \"{oldPath}\" to \"{path}\"/{ParentId}/{Id}, Attributes=({Attributes}), LastWriteTime={LastWriteTime:O}, Size={Size}",
            _volumeId,
            _scope,
            entry.ChangeType,
            ToType(entry.Attributes),
            entry.OldPath,
            entry.Path,
            entry.ParentId,
            entry.Id,
            entry.Attributes,
            entry.LastWriteTimeUtc,
            entry.Size ?? entry.SizeOnStorage);
    }

    private void LogError(EventLogEntry<TId> entry)
    {
        _logger.LogInformation("Event received on {Volume}/\"{Scope}\": {changeType}", _volumeId, _scope, entry.ChangeType);
    }
}
