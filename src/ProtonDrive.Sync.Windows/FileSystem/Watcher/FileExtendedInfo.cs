using System;
using System.IO;

namespace ProtonDrive.Sync.Windows.FileSystem.Watcher;

internal struct FileExtendedInfo
{
    public DateTime CreationTimeUtc;
    public DateTime LastModificationTimeUtc;
    public DateTime LastChangeTimeUtc;
    public DateTime LastAccessTimeUtc;
    public long FileSize;
    public FileAttributes Attributes;
    public long FileId;
    public long ParentFileId;
    public uint ReparseTag;
}
