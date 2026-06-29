using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Proton.Drive.App.Windows.Interop;
using Proton.Drive.Shared.IO;

namespace Proton.Drive.App.Windows.SystemIntegration;

internal sealed class Win32FileSystemDisplayNameAndIconProvider : IFileSystemDisplayNameAndIconProvider
{
    private readonly ConcurrentDictionary<ShellIconSize, ConcurrentDictionary<int, ImageSource?>> _iconsCache = new();

    public bool TryGetDisplayNameAndIcon(string path, ShellIconSize iconSize, [MaybeNullWhen(false)] out string displayName, [MaybeNullWhen(false)] out ImageSource icon)
    {
        var result = GetDisplayNameAndIcon(path, iconSize);

        if (!result.HasValue)
        {
            displayName = null;
            icon = null;
            return false;
        }

        displayName = result.Value.DisplayName;
        icon = result.Value.Icon;

        return true;
    }

    public (string DisplayName, ImageSource Icon)? GetDisplayNameAndIcon(string nameOrPath, ShellIconSize iconSize)
    {
        var itemIdListPointer = Shell32.ILCreateFromPath(nameOrPath);

        var flags = GetIconSizeFlags(iconSize) | Shell32.SHGFI.SysIconIndex | Shell32.SHGFI.PIDL | Shell32.SHGFI.DisplayName;

        try
        {
            return GetDisplayNameAndIconFromItemPointer(itemIdListPointer, fileAttributes: default, flags, iconSize);
        }
        finally
        {
            Shell32.ILFree(itemIdListPointer);
        }
    }

    public ImageSource? GetFileIconWithoutAccess(string nameOrPath, ShellIconSize iconSize)
    {
        var flags = GetIconSizeFlags(iconSize) | Shell32.SHGFI.UseFileAttributes;

        return GetDisplayNameAndIconFromPath(nameOrPath, fileAttributes: default, flags, iconSize)?.Icon;
    }

    public ImageSource? GetFolderIconWithoutAccess(string nameOrPath, ShellIconSize iconSize)
    {
        return GetDisplayNameAndIconFromPath(
            nameOrPath,
            FileAttributes.Directory,
            GetIconSizeFlags(iconSize) | Shell32.SHGFI.UseFileAttributes,
            iconSize)?.Icon;
    }

    public string? GetDisplayNameWithoutAccess(string path)
    {
        var displayName = PathExtensions.GetDisplayNameWithoutAccess(path);

        return displayName.IsEmpty ? null : displayName.ToString();
    }

    private static Shell32.SHGFI GetIconSizeFlags(ShellIconSize iconSize)
    {
        return iconSize switch
        {
            ShellIconSize.Small => Shell32.SHGFI.SmallIcon,
            ShellIconSize.Large => Shell32.SHGFI.LargeIcon,
            _ => throw new ArgumentOutOfRangeException(nameof(iconSize), iconSize, null),
        };
    }

    private (string DisplayName, ImageSource Icon)? GetDisplayNameAndIconFromPath(string path, FileAttributes fileAttributes, Shell32.SHGFI flags, ShellIconSize iconSize)
    {
        var info = new Shell32.SHFILEINFOW(true);

        var cbFileInfo = Marshal.SizeOf(info);

        var systemImageListHandle = Shell32.SHGetFileInfoW(
            path,
            fileAttributes,
            ref info,
            (uint)cbFileInfo,
            flags | Shell32.SHGFI.SysIconIndex);

        if (systemImageListHandle == IntPtr.Zero)
        {
            return null;
        }

        var icon = GetOrAddIcon(iconSize, info.iIcon, systemImageListHandle);

        return icon is null ? default : (info.szDisplayName, icon);
    }

    private (string DisplayName, ImageSource Icon)? GetDisplayNameAndIconFromItemPointer(IntPtr itemIdListPointer, FileAttributes fileAttributes, Shell32.SHGFI flags, ShellIconSize iconSize)
    {
        var info = new Shell32.SHFILEINFOW(true);

        var cbFileInfo = Marshal.SizeOf(info);

        var systemImageListHandle = Shell32.SHGetFileInfoW(
            itemIdListPointer,
            fileAttributes,
            ref info,
            (uint)cbFileInfo,
            flags);

        if (systemImageListHandle == IntPtr.Zero)
        {
            return null;
        }

        var icon = GetOrAddIcon(iconSize, info.iIcon, systemImageListHandle);

        return icon is null ? default : (info.szDisplayName, icon);
    }

    private ImageSource? GetOrAddIcon(ShellIconSize iconSize, int indexIcon, IntPtr systemImageListHandle)
    {
        var cache = _iconsCache.GetOrAdd(iconSize, _ => new ConcurrentDictionary<int, ImageSource?>());

        return cache.GetOrAdd(
            key: indexIcon,
            _ =>
            {
                var iconHandle = Comctl32.ImageList_GetIcon(systemImageListHandle, indexIcon, Comctl32.IMAGELISTDRAWFLAGS.ILD_TRANSPARENT);

                if (iconHandle == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    var result = Imaging.CreateBitmapSourceFromHIcon(iconHandle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    result.Freeze();
                    return result;
                }
                finally
                {
                    User32.DestroyIcon(iconHandle);
                }
            });
    }
}
