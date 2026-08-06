using System.ComponentModel;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Windows.FileSystem.Watcher;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Client;

internal class EventLogClient : IRootableEventLogClient<long>
{
    private static readonly IReadOnlyCollection<EventLogEntry<long>> SkippedEventLogEntry = [new(EventLogChangeType.Skipped)];

    private readonly FileSystemExtendedWatcher _watcher;

    public EventLogClient()
    {
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
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, 0, includeObjectId: false, out var mappedException))
        {
            throw mappedException;
        }
    }

    public void Disable()
    {
        _watcher.EnableRaisingEvents = false;
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

    private void Watcher_OnNextEvents(object sender, IReadOnlyCollection<RenamedExtendedEventArgs> e)
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

        OnNextEntries(ToEventLogEntries(e));
    }

    private void Watcher_OnError(object sender, ErrorExtendedEventArgs e)
    {
        /* File system event log observation has failed, the track of changes is lost.
        // Error is reported also when the directory being monitored gets deleted.
        // Consider locking the directory being monitored so that it could not be renamed or deleted.*/

        OnNextEntries(SkippedEventLogEntry);
    }

    private void OnNextEntries(IReadOnlyCollection<EventLogEntry<long>> entries)
    {
        var eventArgs = new EventLogEntriesReceivedEventArgs<long>(entries);
        LogEntriesReceived?.Invoke(this, eventArgs);
    }

    private static IReadOnlyCollection<EventLogEntry<long>> ToEventLogEntries(IReadOnlyCollection<RenamedExtendedEventArgs> e)
    {
        var result = new List<EventLogEntry<long>>(e.Count);
        result.AddRange(e.Select(ToEventLogEntry));

        return result.AsReadOnly();
    }

    private static EventLogEntry<long> ToEventLogEntry(RenamedExtendedEventArgs e)
    {
        return new EventLogEntry<long>(ToEventLogChangeType(e.ChangeType))
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
}
