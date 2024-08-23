using System.IO;

namespace ProtonDrive.Shared.IO;

public static class FileAttributesExtensions
{
    // FILE_ATTRIBUTE_PINNED
    private const int PinnedAttribute = 0x00080000;

    // FILE_ATTRIBUTE_UNPINNED
    private const int UnpinnedAttribute = 0x00100000;

    public static bool IsPinned(this FileAttributes attributes)
    {
        return attributes.HasFlag((FileAttributes)PinnedAttribute) && !attributes.HasFlag((FileAttributes)UnpinnedAttribute);
    }

    public static bool IsDehydrationRequested(this FileAttributes attributes)
    {
        return attributes.HasFlag((FileAttributes)UnpinnedAttribute) && !attributes.HasFlag((FileAttributes)PinnedAttribute);
    }

    public static bool IsExcluded(this FileAttributes attributes)
    {
        return attributes.HasFlag((FileAttributes)PinnedAttribute | (FileAttributes)UnpinnedAttribute);
    }
}
