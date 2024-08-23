using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace ProtonDrive.Sync.Windows.FileSystem.Client;

public sealed class SharedWithMeRootFolderEntriesFilter : IEventLogClientEntriesFilter
{
    private readonly string _sharedWithMeRootFolderPath;
    private readonly IReadOnlySet<string> _sharedWithMeItemPaths;

    public SharedWithMeRootFolderEntriesFilter(string sharedWithMeRootFolderPath, IReadOnlySet<string> sharedWithMeItemPaths)
    {
        _sharedWithMeRootFolderPath = sharedWithMeRootFolderPath;
        _sharedWithMeItemPaths = sharedWithMeItemPaths;
    }

    /// <summary>
    /// Ignore entries under the "Shared with me" folder which are not mapped/synced.
    /// Only shared with me items must be considered.
    /// </summary>
    /// <param name="eventEntryPath">Entry path</param>
    /// <returns>Returns whether the entry must be ignored</returns>
    public bool EntryMustBeIgnored(string eventEntryPath)
    {
        if (!eventEntryPath.StartsWith(_sharedWithMeRootFolderPath)
            || eventEntryPath.Length < _sharedWithMeRootFolderPath.Length + 1)
        {
            return false;
        }

        var separatorIndex = eventEntryPath.IndexOf(
            Path.DirectorySeparatorChar,
            _sharedWithMeRootFolderPath.Length + 1,
            eventEntryPath.Length - _sharedWithMeRootFolderPath.Length - 1);

        var sharedWithMeItemPath = separatorIndex == -1
            ? eventEntryPath
            : eventEntryPath[..separatorIndex];

        var mustBeIgnored = !_sharedWithMeItemPaths.Contains(sharedWithMeItemPath);
        return mustBeIgnored;
    }

    public bool TryGetRenameEventReplacementChangeType(string newPath, string oldPath, [NotNullWhen(true)] out WatcherChangeTypes? changeType)
    {
        if (_sharedWithMeItemPaths.Contains(newPath))
        {
            changeType = WatcherChangeTypes.Created;
            return true;
        }

        if (_sharedWithMeItemPaths.Contains(oldPath))
        {
            changeType = WatcherChangeTypes.Deleted;
            return true;
        }

        changeType = default;
        return false;
    }
}
