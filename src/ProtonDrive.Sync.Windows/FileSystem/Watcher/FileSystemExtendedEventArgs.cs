using System;
using System.IO;

namespace ProtonDrive.Sync.Windows.FileSystem.Watcher;

/// <summary>
/// Provides data for the <see cref='FileSystemExtendedWatcher.Changed'/>,
/// <see cref='FileSystemExtendedWatcher.Created'/>, or
/// <see cref='FileSystemExtendedWatcher.Deleted'/> event.
/// </summary>
public class FileSystemExtendedEventArgs : FileSystemEventArgs
{
    private readonly FileExtendedInfo _extendedInfo;

    public FileSystemExtendedEventArgs(WatcherChangeTypes changeType, string directory, string? name)
        : base(changeType, directory, name)
    {
    }

    internal FileSystemExtendedEventArgs(
        WatcherChangeTypes changeType,
        string directory,
        string? name,
        FileExtendedInfo extendedInfo)
        : base(changeType, directory, name)
    {
        _extendedInfo = extendedInfo;
    }

    public DateTime CreationTimeUtc => _extendedInfo.CreationTimeUtc;
    public DateTime LastModificationTimeUtc => _extendedInfo.LastModificationTimeUtc;
    public DateTime LastChangeTimeUtc => _extendedInfo.LastChangeTimeUtc;
    public DateTime LastAccessTimeUtc => _extendedInfo.LastAccessTimeUtc;
    public long FileSize => _extendedInfo.FileSize;
    public FileAttributes Attributes => _extendedInfo.Attributes;
    public long FileId => _extendedInfo.FileId;
    public long ParentFileId => _extendedInfo.ParentFileId;
    public uint ReparseTag => _extendedInfo.ReparseTag;
}
