using System;
using System.IO;
using System.Linq;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Sync.Windows.FileSystem.Client;

internal static class FileSystemNodeInfoProvider
{
    private static readonly EnumerationOptions GetInfoOptions = new()
    {
        AttributesToSkip = default,
        ReturnSpecialDirectories = false,
        BufferSize = 1024,
    };

    public static NodeInfo<long> Convert(NodeInfo<long> info)
    {
        var entryName = Path.GetFileName(info.Path.AsSpan());
        var isRootDirectory = entryName.IsEmpty;

        return !isRootDirectory
            ? GetDirectoryEntryInfo(info, entryName)
            : GetDirectoryInfo(info);
    }

    private static NodeInfo<long> GetDirectoryEntryInfo(NodeInfo<long> info, ReadOnlySpan<char> entryName)
    {
        using var parentDirectory = info.OpenParentDirectory(FileSystemFileAccess.ReadData, FileShare.ReadWrite);

        var entry = parentDirectory.EnumerateFileSystemEntries(new string(entryName), GetInfoOptions, ownsHandle: false)
            .WithExceptionMapping(info.Id)
            .SingleOrDefault();

        if (entry == null)
        {
            throw new FileSystemClientException<long>(
                $"Could not find file system object with Id={info.Id}",
                FileSystemErrorCode.PathNotFound,
                info.Id);
        }

        entry.ThrowIfIdentityMismatch(info.Id);

        return entry.ToNodeInfo(parentDirectory.ObjectId);
    }

    private static NodeInfo<long> GetDirectoryInfo(NodeInfo<long> directoryInfo)
    {
        using var directory = directoryInfo.OpenAsDirectory(FileSystemFileAccess.ReadAttributes, FileShare.ReadWrite);
        return directory.ToNodeInfo(parentId: default, refresh: false);
    }
}
