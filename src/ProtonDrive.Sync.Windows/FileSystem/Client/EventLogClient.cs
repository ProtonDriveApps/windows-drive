using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using Microsoft.Extensions.Logging;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Windows.FileSystem.Watcher;

namespace ProtonDrive.Sync.Windows.FileSystem.Client;

public class EventLogClient : IRootableEventLogClient<long>
{
    private static readonly IReadOnlyCollection<EventLogEntry<long>> SkippedEventLogEntry = [new EventLogEntry<long>(EventLogChangeType.Skipped)];

    private readonly IEventLogClientEntriesFilter _entriesFilter;
    private readonly FileSystemExtendedWatcher _watcher;
    private readonly ConcurrentQueue<RenamedExtendedEventArgs> _eventArgsBuffer = new();
    private readonly ILogger<EventLogClient> _logger;

    public EventLogClient(IEventLogClientEntriesFilter entriesFilter, ILogger<EventLogClient> logger)
    {
        _logger = logger;
        _entriesFilter = entriesFilter;
        _watcher = CreateWatcher();
    }

    public event EventHandler<EventLogEntriesReceivedEventArgs<long>>? LogEntriesReceived;

    public void Enable(IRootDirectory<long> rootDirectory)
    {
        try
        {
            /* Enabling watcher with ReadWrite ShareMode prevents the directory being monitored from
            // manipulations including deletion. In other case, if the directory being monitored gets renamed,
            // the FileSystemWatcher continues to report changes in the renamed directory using old value
            // of the path passed to the FileSystemWatcher.*/

            // TODO: Consider checking the identity of the root directory before enabling watcher.
            _watcher.Path = rootDirectory.Path;
            OnNextEntries(SkippedEventLogEntry);
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, default, includeObjectId: false, out var mappedException))
        {
            throw mappedException;
        }
    }

    public void Disable()
    {
        _watcher.EnableRaisingEvents = false;
    }

    private static EventLogChangeType ToEventLogChangeType(WatcherChangeTypes value)
    {
        return value switch
        {
            WatcherChangeTypes.Created => EventLogChangeType.CreatedOrMovedTo,
            WatcherChangeTypes.Changed => EventLogChangeType.Changed,
            WatcherChangeTypes.Renamed => EventLogChangeType.Moved,
            WatcherChangeTypes.Deleted => EventLogChangeType.DeletedOrMovedFrom,
            _ => throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(WatcherChangeTypes)),
        };
    }

    private FileSystemExtendedWatcher CreateWatcher()
    {
        var watcher = new FileSystemExtendedWatcher
        {
            NotifyFilter = NotifyFilters.FileName |
                           NotifyFilters.DirectoryName |
                           NotifyFilters.Attributes |
                           NotifyFilters.Size |
                           NotifyFilters.LastWrite,
            IncludeSubdirectories = true,
            InternalBufferSize = 64 * 1024,

            // The directory being watched is shared for reading and writing. It cannot be manipulated
            // (renamed, moved or deleted) while being monitored.
            ShareMode = FileShare.ReadWrite,
        };

        // Events are raised on a system thread pool thread.
        watcher.NextEvents += Watcher_OnNextEvents;
        watcher.Error += Watcher_OnError;

        return watcher;
    }

    private void Watcher_OnNextEvents(object sender, IReadOnlyCollection<RenamedExtendedEventArgs> eventArgsCollection)
    {
        /* File or directory is created, changed or deleted in the directory being monitored.
        // File or directory is renamed within the directory being monitored. If file or directory
        // is moved into the directory being monitored, the creation event is reported. If
        // moved from within into outside the directory being monitored, the deletion event is
        // reported.
        // Changes to the directory being monitored are not reported.

        // On Windows changes might be reported using short path name rather than long path name.
        // For example, when file with the full name longer than MAX_PATH (256) gets deleted
        // by Windows Explorer, it's deletion event contains short path name. If the short path name
        // is longer than MAX_PATH (256), then deletion event contains long file name.

        // On Windows file or directory rename which include parent directory change is reported as
        // deletion and creation instead of rename. On Linux it is reported as rename.

        // On Linux file replace is reported as rename into target file name. The deletion of the
        // target file is nor reported.

        // If the directory being monitored gets renamed, the FileSystemWatcher continues monitoring
        // the renamed directory. However, the reported paths become incorrect as they still
        // include the path initially set to the FileSystemWatcher Path property.
        // Consider locking the directory being monitored so that it could not be renamed or deleted.

        // On Windows the rename might be reported with null value in OldName or Name.*/

        OnNextEntries(ToEventLogEntries(eventArgsCollection));
    }

    private void Watcher_OnError(object sender, ErrorExtendedEventArgs e)
    {
        /* File system event log observation has failed, the track of changes is lost.
        // Error is reported also when the directory being monitored gets deleted.
        // Consider locking the directory being monitored so that it could not be renamed or deleted.*/

        OnNextEntries(SkippedEventLogEntry);
    }

    private ReadOnlyCollection<EventLogEntry<long>> ToEventLogEntries(IReadOnlyCollection<RenamedExtendedEventArgs> eventArgs)
    {
        var result = new List<EventLogEntry<long>>(eventArgs.Count);

        foreach (var entry in eventArgs)
        {
            var oldName = Path.GetFileName(entry.OldName);
            var oldPath = entry.OldName is not null ? Path.Combine(entry.DirectoryPath, entry.OldName) : null;

            if (_entriesFilter.EntryMustBeIgnored(entry.FullPath) && (oldPath is null || _entriesFilter.EntryMustBeIgnored(oldPath)))
            {
                continue;
            }

            result.Add(ToEventLogEntry(entry, oldName, oldPath));
        }

        return result.AsReadOnly();
    }

    private EventLogEntry<long> ToEventLogEntry(RenamedExtendedEventArgs e, string? oldName, string? oldPath)
    {
        var isFileRename = !e.Attributes.HasFlag(FileAttributes.Directory) && e.ChangeType is WatcherChangeTypes.Renamed;

        var changeType = isFileRename
            && oldPath is not null
            && _entriesFilter.TryGetRenameEventReplacementChangeType(e.FullPath, oldPath, out var ct)
                ? ct.Value
                : e.ChangeType;

        var result = new EventLogEntry<long>(ToEventLogChangeType(changeType))
        {
            Name = Path.GetFileName(e.Name),
            Path = e.Name,
            OldPath = e.OldName,
            Id = e.FileId,
            ParentId = e.ParentFileId,
            Attributes = e.Attributes,
            LastWriteTimeUtc = e.LastModificationTimeUtc,
            Size = e.FileSize,
            PlaceholderState = Internal.FileSystem.GetPlaceholderState(e.Attributes, e.ReparseTag),
        };

        if (changeType != e.ChangeType)
        {
            _logger.LogDebug("Event replacement: {NewChangeType} (from {OldChangeType}) for file {FileId}", changeType, e.ChangeType, e.FileId);

            result = changeType switch
            {
                WatcherChangeTypes.Created => result with { OldPath = default },
                WatcherChangeTypes.Deleted => result with { Name = oldName, Path = e.OldName, },
                _ => result,
            };
        }

        return result;
    }

    private void OnNextEntries(IReadOnlyCollection<EventLogEntry<long>> entries)
    {
        var eventArgs = new EventLogEntriesReceivedEventArgs<long>(entries);
        LogEntriesReceived?.Invoke(this, eventArgs);
    }
}
