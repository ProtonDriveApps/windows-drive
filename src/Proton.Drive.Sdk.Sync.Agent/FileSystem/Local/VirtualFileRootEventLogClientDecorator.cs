using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Local;

internal sealed class VirtualFileRootEventLogClientDecorator : IEventLogClient<long>
{
    private readonly long _parentFolderId;
    private readonly string _rootFileName;
    private readonly IRootableEventLogClient<long> _decoratedInstance;

    public VirtualFileRootEventLogClientDecorator(long parentFolderId, string rootFileName, IRootableEventLogClient<long> instanceToDecorate)
    {
        _parentFolderId = parentFolderId;
        _rootFileName = rootFileName;
        _decoratedInstance = instanceToDecorate;

        _decoratedInstance.LogEntriesReceived += OnDecoratedInstanceLogEntriesReceived;
    }

    public event EventHandler<EventLogEntriesReceivedEventArgs<long>>? LogEntriesReceived;

    public void Enable()
    {
        // Do not call decorated instance
    }

    public void Disable()
    {
        // Do not call decorated instance
    }

    public Task GetEventsAsync()
    {
        // Do not call decorated instance
        return Task.CompletedTask;
    }

    private void OnDecoratedInstanceLogEntriesReceived(object? sender, EventLogEntriesReceivedEventArgs<long> eventArgs)
    {
        var transformedEntries = eventArgs.Entries.SelectMany(ToTransformedEntry).ToList().AsReadOnly();

        LogEntriesReceived?.Invoke(this, new EventLogEntriesReceivedEventArgs<long>(transformedEntries)
        {
            VolumeId = eventArgs.VolumeId,
            Scope = eventArgs.Scope,
        });
    }

    private IEnumerable<EventLogEntry<long>> ToTransformedEntry(EventLogEntry<long> entry)
    {
        if (entry.ChangeType is EventLogChangeType.Error or EventLogChangeType.Skipped)
        {
            // We skip error entries, as the virtual file root is on the same event scope as
            // other roots (cloud files and host device folders). Therefore, there is no need to duplicate
            // error entries for the virtual volume, the file system adapter will receive those entries anyway.
            yield break;
        }

        if (!entry.ParentId.Equals(_parentFolderId))
        {
            // We skip all entries outside the parent folder ("Shared with me").
            // If it was a move from the parent folder to a different folder, we will
            // still process DeletedOrMovedFrom entry, but will skip CreatedOrMovedTo entry.
            yield break;
        }

        if (entry.Attributes.HasFlag(FileAttributes.Directory))
        {
            // We skip folder entries, as we are interested only in files
            yield break;
        }

        // We filter by file name, as file ID can change due to replacing with a new file version
        // when a temporary file is involved
        if (_rootFileName.Equals(entry.Name, StringComparison.Ordinal))
        {
            // A rename without changing parent folder is received as a single entry of Moved type
            if (entry.ChangeType is EventLogChangeType.Moved)
            {
                // We replace renaming to the expected name with creation
                yield return entry with
                {
                    ChangeType = EventLogChangeType.CreatedOrMovedTo,
                };
            }
            else
            {
                yield return entry;
            }

            yield break;
        }

        if (IsRename(entry))
        {
            // We replace renaming with deletion
            yield return entry with
            {
                ChangeType = EventLogChangeType.DeletedOrMovedFrom,
            };
        }
    }

    private bool IsRename(EventLogEntry<long> entry)
    {
        // A rename without changing parent folder is received as a single entry of Moved type
        if (entry.ChangeType is not EventLogChangeType.Moved)
        {
            return false;
        }

        var oldName = Path.GetFileName(entry.OldPath);
        if (string.IsNullOrEmpty(oldName))
        {
            return false;
        }

        return _rootFileName.Equals(oldName, StringComparison.Ordinal);
    }
}
