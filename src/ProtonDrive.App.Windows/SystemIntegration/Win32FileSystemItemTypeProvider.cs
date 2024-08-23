using System;
using System.IO;
using System.Runtime.InteropServices;
using ProtonDrive.App.Windows.Interop;

namespace ProtonDrive.App.Windows.SystemIntegration;

internal sealed class Win32FileSystemItemTypeProvider : IFileSystemItemTypeProvider
{
    private const int Failed = 0;

    public string? GetFileType(string filename)
    {
        return GetFileSystemItemType(filename, FileAttributes.Normal);
    }

    public string? GetFolderType()
    {
        // Providing an empty string as a filename produces description of the local disk
        // ("Local Disk"). Therefore, we are providing a dummy name "Folder".
        return GetFileSystemItemType("Folder", FileAttributes.Directory);
    }

    private string? GetFileSystemItemType(string filename, FileAttributes fileAttributes)
    {
        var info = new Shell32.SHFILEINFOW(true);

        var cbFileInfo = Marshal.SizeOf(info);

        if (Shell32.SHGetFileInfoW(
                filename,
                fileAttributes,
                ref info,
                (uint)cbFileInfo,
                Shell32.SHGFI.UseFileAttributes | Shell32.SHGFI.TypeName) == IntPtr.Zero)
        {
            return null;
        }

        return info.szTypeName;
    }
}
