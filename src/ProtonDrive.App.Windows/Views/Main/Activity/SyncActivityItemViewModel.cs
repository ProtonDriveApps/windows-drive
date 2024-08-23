using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.App.Windows.SystemIntegration;
using ProtonDrive.App.Windows.Toolkit.Converters;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.SyncActivity;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.App.Windows.Views.Main.Activity;

internal sealed class SyncActivityItemViewModel : ObservableObject
{
    private static readonly EnumToDisplayTextConverter EnumToDisplayTextConverter = EnumToDisplayTextConverter.Instance;

    private readonly IFileSystemDisplayNameAndIconProvider _fileSystemDisplayNameAndIconProvider;
    private readonly ILocalFolderService _localFolderService;

    private SyncActivityItem<long> _dataItem;
    private SyncActivityItemStatus _status;
    private DateTime? _synchronizedAt;
    private FileSystemErrorCode _errorCode;
    private string? _errorMessage;
    private Progress _progress;

    public SyncActivityItemViewModel(
        SyncActivityItem<long> dataItem,
        IFileSystemDisplayNameAndIconProvider fileSystemDisplayNameAndIconProvider,
        ILocalFolderService localFolderService)
    {
        _dataItem = dataItem;
        _fileSystemDisplayNameAndIconProvider = fileSystemDisplayNameAndIconProvider;
        _localFolderService = localFolderService;

        OpenFolderCommand = new AsyncRelayCommand(OpenFolderAsync, CanOpenFolder);

        OnDataItemUpdated(dataItem);
    }

    public ICommand OpenFolderCommand { get; }

    public Replica Replica => DataItem.Replica;

    public SyncActivityItemStatus Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(ActivityTypeDisplayText));
            }
        }
    }

    public bool ProgressIsIndeterminate => _dataItem.ActivityType is not (SyncActivityType.Upload or SyncActivityType.Download);

    public ImageSource? Icon => _dataItem.NodeType is NodeType.Directory
        ? _fileSystemDisplayNameAndIconProvider.GetFolderIconWithoutAccess(Name, ShellIconSize.Small)
        : _fileSystemDisplayNameAndIconProvider.GetFileIconWithoutAccess(Name, ShellIconSize.Small);

    public string Name => DataItem.Name;

    public Progress Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }

    public NodeType NodeType => DataItem.NodeType;

    public string FolderName => GetFolderName();

    public string FolderPath => GetFolderPath();

    public long? Size => DataItem.Size;

    public string ActivityTypeDisplayText => GetActivityTypeDisplayText();

    public DateTime? SynchronizedAt
    {
        get => _synchronizedAt;
        set => SetProperty(ref _synchronizedAt, value);
    }

    public FileSystemErrorCode ErrorCode
    {
        get => _errorCode;
        private set
        {
            if (SetProperty(ref _errorCode, value))
            {
                OnPropertyChanged();
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged();
            }
        }
    }

    internal SyncActivityItem<long> DataItem
    {
        get => _dataItem;
        set
        {
            _dataItem = value;
            OnDataItemUpdated(value);
        }
    }

    public void OnSynchronizedAtChanged()
    {
        OnPropertyChanged(nameof(SynchronizedAt));
    }

    private static string GetResourceKeyPattern(SyncActivityItemStatus status)
    {
        const string type = EnumToDisplayTextConverter.TypeNamePlaceholder;
        const string value = EnumToDisplayTextConverter.ValueNamePlaceholder;

        return (status != SyncActivityItemStatus.Succeeded)
            ? $"Activity_InProgress_{type}_val_{value}"
            : $"Activity_Succeeded_{type}_val_{value}";
    }

    private void OnDataItemUpdated(SyncActivityItem<long> value)
    {
        // Only some properties of data item can change
        Status = value.Status;
        ErrorCode = value.ErrorCode;
        ErrorMessage = value.ErrorMessage;
        Progress = value.Progress;
        OnPropertyChanged(nameof(Size));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Replica));
    }

    private string GetActivityTypeDisplayText()
    {
        var activityType = DataItem.ActivityType;

        var converted = EnumToDisplayTextConverter.Convert(
            value: activityType,
            targetType: null,
            parameter: GetResourceKeyPattern(Status),
            culture: null);

        var activityText = converted as string ?? string.Empty;

        activityText += activityType switch
        {
            SyncActivityType.Upload => " to Proton Drive",
            SyncActivityType.Download => " from Proton Drive",
            SyncActivityType.Create => Replica == Replica.Remote ? " on Proton Drive" : string.Empty,
            SyncActivityType.Rename => Replica == Replica.Remote ? " on Proton Drive" : string.Empty,
            SyncActivityType.Move => Replica == Replica.Remote ? " on Proton Drive" : string.Empty,
            SyncActivityType.Delete => Replica == Replica.Remote ? " from Proton Drive" : string.Empty,
            SyncActivityType.FetchUpdates => Replica == Replica.Remote ? " from Proton Drive" : string.Empty,
            _ => string.Empty,
        };

        return activityText;
    }

    private bool CanOpenFolder()
    {
        return !string.IsNullOrEmpty(_dataItem.LocalRootPath);
    }

    private async Task OpenFolderAsync()
    {
        var folderPath = GetFolderPath();

        await _localFolderService.OpenFolderAsync(folderPath).ConfigureAwait(true);
    }

    private string GetFolderName()
    {
        var relativeFolderPath = _dataItem.RelativeParentFolderPath;

        if (!string.IsNullOrEmpty(relativeFolderPath))
        {
            return Path.GetFileName(relativeFolderPath);
        }

        // We are on the sync root folder, taking the name of it
        var folderName = Path.GetFileName(_dataItem.LocalRootPath);

        if (string.IsNullOrEmpty(folderName))
        {
            // The sync root is the root of the volume
            var rootName = Path.GetPathRoot(_dataItem.LocalRootPath) ?? string.Empty;

            // Stripping the ending path separator from the drive letter ("X:\")
            return Path.EndsInDirectorySeparator(rootName) ? rootName[..^1] : rootName;
        }

        return folderName;
    }

    private string GetFolderPath()
    {
        var relativeFolderPath = _dataItem.RelativeParentFolderPath;

        return Path.Combine(_dataItem.LocalRootPath, relativeFolderPath);
    }
}
