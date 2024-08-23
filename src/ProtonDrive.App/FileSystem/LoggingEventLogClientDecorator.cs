using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem;

internal sealed class LoggingEventLogClientDecorator<TId> : IEventLogClient<TId>
{
    private readonly ILogger<LoggingEventLogClientDecorator<TId>> _logger;
    private readonly IEventLogClient<TId> _origin;
    private readonly int _volumeId;
    private readonly string _scope;

    public LoggingEventLogClientDecorator(
        ILogger<LoggingEventLogClientDecorator<TId>> logger,
        IEventLogClient<TId> origin,
        int volumeId,
        string scope)
    {
        _logger = logger;
        _origin = origin;
        _volumeId = volumeId;
        _scope = scope;

        _origin.LogEntriesReceived += OnOriginLogEntriesReceived;
    }

    public event EventHandler<EventLogEntriesReceivedEventArgs<TId>>? LogEntriesReceived;

    public void Enable()
    {
        _logger.LogInformation("Enabling directory change observation on {Volume}/\"{Scope}\"", _volumeId, _scope);

        _origin.Enable();
    }

    public void Disable()
    {
        _logger.LogInformation("Disabling directory change observation on {Volume}/\"{Scope}\"", _volumeId, _scope);

        _origin.Disable();
    }

    public Task GetEventsAsync() => _origin.GetEventsAsync();

    private static string ToType(FileAttributes attributes)
    {
        return attributes.HasFlag(FileAttributes.Directory)
            ? "Directory"
            : "File";
    }

    private void OnOriginLogEntriesReceived(object? sender, EventLogEntriesReceivedEventArgs<TId> e)
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
