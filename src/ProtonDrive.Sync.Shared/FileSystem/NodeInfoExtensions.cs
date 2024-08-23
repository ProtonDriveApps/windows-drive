using System;
using System.IO;

namespace ProtonDrive.Sync.Shared.FileSystem;

public static class NodeInfoExtensions
{
    public static bool IsDirectory<TId>(this NodeInfo<TId> origin)
        where TId : IEquatable<TId>
    {
        return (origin.Attributes & FileAttributes.Directory) != 0;
    }

    public static bool IsFile<TId>(this NodeInfo<TId> origin)
        where TId : IEquatable<TId>
    {
        return !IsDirectory(origin);
    }

    public static string? GetParentFolderPath<TId>(this NodeInfo<TId>? info)
        where TId : IEquatable<TId>
    {
        if (info == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(info.Name) && info.Path.Length >= info.Name.Length)
        {
            return Path.TrimEndingDirectorySeparator(info.Path[..^info.Name.Length]);
        }

        return Path.GetDirectoryName(info.Path);
    }
}
