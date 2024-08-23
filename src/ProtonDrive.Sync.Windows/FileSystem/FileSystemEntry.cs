using System;
using System.Diagnostics;
using System.IO;
using ProtonDrive.Shared.IO;

namespace ProtonDrive.Sync.Windows.FileSystem;

[DebuggerDisplay("{Name} - Id: {ObjectId} - {Size} bytes")]
public class FileSystemEntry
{
    internal uint ReparseTag { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public string ShortName { get; private set; } = string.Empty;
    public FileAttributes Attributes { get; private set; }
    public DateTime CreationTimeUtc { get; private set; }
    public DateTime LastWriteTimeUtc { get; private set; }
    public long Size { get; private set; }
    public long ObjectId { get; private set; }
    public PlaceholderState PlaceholderState { get; private set; }

    internal unsafe void Initialize(Interop.NtDll.FILE_ID_BOTH_DIR_INFORMATION* entry)
    {
        // The FILE_ID_BOTH_DIR_INFORMATION structure contains reparse tag instead of extended attributes size
        // if the file or folder is reparse point. Reparse points cannot have extended attributes.
        ReparseTag = entry->EaSize;

        Name = entry->FileName.ToString();
        ShortName = entry->ShortNameLength != 0 ? entry->ShortName.ToString() : string.Empty;
        Attributes = (FileAttributes)entry->FileAttributes;
        CreationTimeUtc = entry->CreationTime.ToDateTimeUtc();
        LastWriteTimeUtc = entry->LastWriteTime.ToDateTimeUtc();
        Size = unchecked((long)entry->EndOfFile);
        ObjectId = unchecked((long)entry->FileId);
        PlaceholderState = Internal.FileSystem.GetPlaceholderState(Attributes, ReparseTag);
    }
}
