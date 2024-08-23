using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace ProtonDrive.Sync.Windows.FileSystem.Client;

public interface IEventLogClientEntriesFilter
{
    public bool EntryMustBeIgnored(string eventEntryPath);
    public bool TryGetRenameEventReplacementChangeType(string newPath, string oldPath, [NotNullWhen(true)] out WatcherChangeTypes? changeType);
}
