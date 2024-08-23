using System.IO;

namespace ProtonDrive.Sync.Windows.FileSystem.Watcher;

/// <summary>
/// Provides data for the <see cref='FileSystemExtendedWatcher.Renamed'/> event.
/// </summary>
public class RenamedExtendedEventArgs : FileSystemExtendedEventArgs
{
    public RenamedExtendedEventArgs(WatcherChangeTypes changeType, string directory, string? name)
        : base(changeType, directory, name)
    {
        DirectoryPath = directory;
    }

    internal RenamedExtendedEventArgs(WatcherChangeTypes changeType, string directory, string? name, FileExtendedInfo extendedInfo)
        : base(changeType, directory, name, extendedInfo)
    {
        DirectoryPath = directory;
    }

    internal RenamedExtendedEventArgs(
        WatcherChangeTypes changeType,
        string directory,
        string? name,
        string? oldName,
        FileExtendedInfo extendedInfo,
        long oldParentFileId)
        : this(changeType, directory, name, extendedInfo)
    {
        OldName = oldName;
        OldParentFileId = oldParentFileId;
    }

    public string? OldName { get; }
    public long OldParentFileId { get; }
    public string DirectoryPath { get; }
}
