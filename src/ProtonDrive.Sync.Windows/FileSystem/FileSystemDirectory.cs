using System.Collections.Generic;
using System.IO;
using Microsoft.Win32.SafeHandles;
using ProtonDrive.Shared;
using ProtonDrive.Sync.Windows.FileSystem.Enumeration;
using ProtonDrive.Sync.Windows.FileSystem.Internal;
using ProtonDrive.Sync.Windows.Interop;

namespace ProtonDrive.Sync.Windows.FileSystem;

public sealed class FileSystemDirectory : FileSystemObject
{
    private const FileShare DefaultShare = FileShare.Read;

    internal FileSystemDirectory(SafeFileHandle handle, string fullPath, FileSystemFileAccess access, bool isAsync)
        : base(handle, fullPath, access, isAsync)
    {
    }

    public static unsafe void Create(string path)
    {
        Ensure.NotNullOrEmpty(path, nameof(path));

        if (!Path.IsPathFullyQualified(path))
        {
            // Path.GetFullPath checks if the path is valid, converts short path to full path if file path exists
            // on the file system, and strips trailing ' ' and '.' characters.
            path = Path.GetFullPath(path);
        }

        path = PathInternal.EnsureExtendedDirectoryPathPrefixIfNeeded(path);

        if (!Kernel32.CreateDirectory(path, null))
        {
            throw Win32Marshal.GetExceptionForLastWin32Error();
        }
    }

    public static FileSystemDirectory Open(string path, FileSystemFileAccess access, FileShare share = DefaultShare)
    {
        return Open(path, access, share, FileOptions.None);
    }

    public static FileSystemDirectory Open(string path, FileSystemFileAccess access, FileShare share, FileOptions options)
    {
        Ensure.NotNullOrEmpty(path, nameof(path));

        Validate(FileMode.Open, access, share, attributes: default, options);

        if (!Path.IsPathFullyQualified(path))
        {
            // Path.GetFullPath checks if the path is valid, converts short path to full path if file path exists
            // on the file system, and strips trailing ' ' and '.' characters.
            path = Path.GetFullPath(path);
        }

        // FILE_FLAG_BACKUP_SEMANTICS is required for opening directory handles
        options |= (FileOptions)Kernel32.FileFlags.FILE_FLAG_BACKUP_SEMANTICS;

        var handle = Internal.FileSystem.CreateHandle(path, FileMode.Open, access, share, attributes: default, options);
        try
        {
            var info = Internal.FileSystem.GetFileInformation(handle);
            if (!info.dwFileAttributes.HasFlag(FileAttributes.Directory))
            {
                throw new TypeMismatchException("The file system object at the given path is not a directory");
            }

            var isAsync = (options & FileOptions.Asynchronous) != 0;

            return new FileSystemDirectory(handle, path, access, isAsync);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    public IEnumerable<FileSystemEntry> EnumerateFileSystemEntries(bool ownsHandle)
    {
        return EnumerateFileSystemEntries(fileName: null, options: null, ownsHandle);
    }

    public IEnumerable<FileSystemEntry> EnumerateFileSystemEntries(string? fileName = null, EnumerationOptions? options = null, bool ownsHandle = false)
    {
        return WithTransferredHandleOwnership(
            handle => new FileSystemEnumerable(handle, ownsHandle, fileName, options),
            ownsHandle);
    }

    public void Delete(bool recursive)
    {
        if (recursive)
        {
            // Fallback to System.IO for recursive directory deletion.
            Directory.Delete(FullPath, true);
        }
        else
        {
            Delete();
        }
    }
}
