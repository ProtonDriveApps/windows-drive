using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace ProtonDrive.Sync.Windows.FileSystem.Client;

public sealed class NullEventLogClientEntriesFilter : IEventLogClientEntriesFilter
{
    private static NullEventLogClientEntriesFilter? _instance;

    private NullEventLogClientEntriesFilter()
    { }

    public static NullEventLogClientEntriesFilter Instance => _instance ??= new NullEventLogClientEntriesFilter();

    public bool EntryMustBeIgnored(string eventEntryPath)
    {
        return false;
    }

    public bool TryGetRenameEventReplacementChangeType(string newPath, string oldPath, [NotNullWhen(true)] out WatcherChangeTypes? changeType)
    {
        changeType = default;
        return false;
    }
}
