using ProtonDrive.Shared.IO;
using Vanara.PInvoke;

namespace ProtonDrive.Sync.Windows.FileSystem;

public static class FileSystemEntryExtensions
{
    public static PlaceholderState GetPlaceholderState(this FileSystemEntry entry)
    {
        return (PlaceholderState)CldApi.CfGetPlaceholderStateFromAttributeTag((FileFlagsAndAttributes)entry.Attributes, entry.ReparseTag);
    }
}
