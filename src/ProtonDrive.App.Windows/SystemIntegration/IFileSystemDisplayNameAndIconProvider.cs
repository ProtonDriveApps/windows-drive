using System.Diagnostics.CodeAnalysis;
using System.Windows.Media;

namespace ProtonDrive.App.Windows.SystemIntegration;

internal interface IFileSystemDisplayNameAndIconProvider
{
    bool TryGetDisplayNameAndIcon(
        string path,
        ShellIconSize iconSize,
        [MaybeNullWhen(false)] out string displayName,
        [MaybeNullWhen(false)] out ImageSource icon);

    (string DisplayName, ImageSource Icon)? GetDisplayNameAndIcon(string path, ShellIconSize iconSize);

    ImageSource? GetFileIconWithoutAccess(string nameOrPath, ShellIconSize iconSize);

    ImageSource? GetFolderIconWithoutAccess(string nameOrPath, ShellIconSize iconSize);
}
