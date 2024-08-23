using System.IO;
using Microsoft.Win32.SafeHandles;
using ProtonDrive.Shared;
using ProtonDrive.Sync.Windows.Interop;

namespace ProtonDrive.Sync.Windows.FileSystem;

public sealed class FileSystemFile : FileSystemObject
{
    private const FileShare DefaultShare = FileShare.Read;
    private const bool DefaultUseAsync = true;
    private const int DefaultBufferSize = 4096;

    public FileSystemFile(SafeFileHandle handle, string fullPath, FileSystemFileAccess access, bool isAsync)
        : base(handle, fullPath, access, isAsync)
    {
    }

    public static FileSystemFile Create(
        string path,
        FileAttributes attributes = default,
        bool useAsync = DefaultUseAsync)
    {
        return Open(
            path,
            FileMode.Create,
            FileSystemFileAccess.ReadWrite,
            DefaultShare,
            attributes,
            useAsync);
    }

    public static FileSystemFile Create(
        string path,
        FileShare share,
        FileAttributes attributes = default,
        bool useAsync = DefaultUseAsync)
    {
        return Open(
            path,
            FileMode.Create,
            FileSystemFileAccess.ReadWrite,
            share,
            attributes,
            useAsync);
    }

    public static FileSystemFile Create(
        string path,
        FileShare share,
        FileOptions options)
    {
        return Open(
            path,
            FileMode.Create,
            FileSystemFileAccess.ReadWrite,
            share,
            attributes: default,
            options);
    }

    public static FileSystemFile Open(string path, FileMode mode, bool useAsync = DefaultUseAsync)
    {
        return Open(
            path,
            mode,
            mode == FileMode.Append ? FileSystemFileAccess.Write : FileSystemFileAccess.ReadWrite,
            DefaultShare,
            attributes: default,
            useAsync);
    }

    public static FileSystemFile Open(
        string path,
        FileSystemFileAccess access,
        FileShare share = DefaultShare,
        bool useAsync = DefaultUseAsync)
    {
        return Open(path, FileMode.Open, access, share, attributes: default, useAsync ? FileOptions.Asynchronous : FileOptions.None);
    }

    public static FileSystemFile Open(
        string path,
        FileMode mode,
        FileSystemFileAccess access,
        FileShare share = DefaultShare,
        FileAttributes attributes = default,
        bool useAsync = DefaultUseAsync)
    {
        return Open(path, mode, access, share, attributes, useAsync ? FileOptions.Asynchronous : FileOptions.None);
    }

    public static FileSystemFile Open(
        string path,
        FileMode mode,
        FileSystemFileAccess access,
        FileShare share,
        FileAttributes attributes,
        FileOptions options,
        SafeFileHandle? templateFileHandle = null)
    {
        Ensure.NotNullOrEmpty(path, nameof(path));

        if (!Path.IsPathFullyQualified(path))
        {
            // Path.GetFullPath checks if the path is valid, converts short path to full path if file path exists
            // on the file system, and strips trailing ' ' and '.' characters.
            path = Path.GetFullPath(path);
        }

        Validate(mode, access, share, attributes, options);

        // FILE_FLAG_BACKUP_SEMANTICS is required for opening directory handles
        options |= (FileOptions)Kernel32.FileFlags.FILE_FLAG_BACKUP_SEMANTICS;

        var fileHandle = Internal.FileSystem.CreateHandle(path, mode, access, share, attributes, options, templateFileHandle);

        try
        {
            var info = Internal.FileSystem.GetFileInformation(fileHandle);
            if (info.dwFileAttributes.HasFlag(FileAttributes.Directory))
            {
                throw new TypeMismatchException("The file system object at the given path is not a file");
            }

            var isAsync = (options & FileOptions.Asynchronous) != 0;
            return new FileSystemFile(fileHandle, path, access, isAsync);
        }
        catch
        {
            fileHandle.Dispose();
            throw;
        }
    }

    public FileSystemFile ReOpen(FileSystemFileAccess access, FileShare share = DefaultShare, bool useAsync = DefaultUseAsync)
    {
        return ReOpen(access, share, attributes: default, useAsync ? FileOptions.Asynchronous : FileOptions.None);
    }

    public FileSystemFile ReOpen(FileSystemFileAccess access, FileShare share, FileAttributes attributes, FileOptions options)
    {
        Validate(FileMode.Open, access, share, attributes, options);

        var fileHandle = ReOpenHandle(FileHandle, access, share, attributes, options);
        try
        {
            var isAsync = (options & FileOptions.Asynchronous) != 0;
            return new FileSystemFile(fileHandle, FullPath, access, isAsync);
        }
        catch
        {
            fileHandle.Dispose();
            throw;
        }
    }

    public FileStream OpenRead(int bufferSize = DefaultBufferSize, bool ownsHandle = true)
    {
        return OpenStream(FileAccess.Read, bufferSize, ownsHandle);
    }

    public FileStream OpenWrite(int bufferSize = DefaultBufferSize, bool ownsHandle = true)
    {
        return OpenStream(FileAccess.Write, bufferSize, ownsHandle);
    }

    public FileStream OpenStream(FileAccess access, int bufferSize = DefaultBufferSize, bool ownsHandle = true)
    {
        return WithTransferredHandleOwnership(
            handle => new FileStream(handle, access, bufferSize, IsAsync),
            ownsHandle);
    }

    public void Truncate()
    {
        Internal.FileSystem.TruncateFile(FileHandle);
    }

    private static SafeFileHandle ReOpenHandle(
        SafeFileHandle handle,
        FileSystemFileAccess access,
        FileShare share,
        FileAttributes attributes,
        FileOptions options)
    {
        return Internal.FileSystem.ReOpenFile(handle, access, share, attributes, options);
    }
}
