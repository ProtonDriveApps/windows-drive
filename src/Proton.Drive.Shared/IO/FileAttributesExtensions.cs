namespace Proton.Drive.Shared.IO;

public static class FileAttributesExtensions
{
    // FILE_ATTRIBUTE_PINNED
    private const FileAttributes PinnedAttribute = (FileAttributes)0x00080000;

    // FILE_ATTRIBUTE_UNPINNED
    private const FileAttributes UnpinnedAttribute = (FileAttributes)0x00100000;

    public static bool IsPinned(this FileAttributes attributes)
    {
        return attributes.HasFlag(PinnedAttribute) && !attributes.HasFlag(UnpinnedAttribute);
    }

    public static bool IsDehydrationRequested(this FileAttributes attributes)
    {
        return attributes.HasFlag(UnpinnedAttribute) && !attributes.HasFlag(PinnedAttribute);
    }

    public static bool IsExcluded(this FileAttributes attributes)
    {
        return attributes.HasFlag(PinnedAttribute | UnpinnedAttribute);
    }
}
