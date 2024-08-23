using System.Diagnostics.CodeAnalysis;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Windows.SystemIntegration;

namespace ProtonDrive.App.Windows.Views.Main.Computers;

internal sealed class SelectableFolderViewModel : ObservableObject
{
    private bool _isChecked;

    private SelectableFolderViewModel(
        string path,
        string name,
        ImageSource? icon,
        bool isChecked)
    {
        Path = path;
        Name = name;
        Icon = icon;
        IsChecked = isChecked;
    }

    public string Path { get; }
    public string Name { get; }
    public ImageSource? Icon { get; }
    public SyncFolderValidationResult ValidationResult { get; set; }

    public bool IsChecked
    {
        get => _isChecked;
        set => SetProperty(ref _isChecked, value);
    }

    public bool IsDisabled { get; set; }

    public static bool TryCreate(
        string path,
        bool isChecked,
        IFileSystemDisplayNameAndIconProvider fileSystemDisplayNameAndIconProvider,
        [MaybeNullWhen(false)] out SelectableFolderViewModel folder)
    {
        var result = fileSystemDisplayNameAndIconProvider.GetDisplayNameAndIcon(path, ShellIconSize.Small);

        if (!result.HasValue)
        {
            folder = default;
            return false;
        }

        folder = new SelectableFolderViewModel(path, result.Value.DisplayName, result.Value.Icon, isChecked);

        return true;
    }
}
