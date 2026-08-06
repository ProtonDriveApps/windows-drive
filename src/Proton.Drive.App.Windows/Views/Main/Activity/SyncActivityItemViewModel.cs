using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Proton.Drive.App.Windows.SystemIntegration;
using Proton.Drive.App.Windows.Toolkit.Converters;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Sdk.Sync.Shared.SyncActivity;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;
using Proton.Drive.Shared.IO;

namespace Proton.Drive.App.Windows.Views.Main.Activity;

internal sealed class SyncActivityItemViewModel : ObservableObject
{
    private readonly IFileSystemDisplayNameAndIconProvider _fileSystemDisplayNameAndIconProvider;
    private readonly ILocalFolderService _localFolderService;

    private SyncActivityItem<long> _dataItem;
    private SyncActivityItemStatus _status;
    private DateTime? _synchronizedAt;
    private FileSystemErrorCode? _errorCode;
    private string? _errorMessage;
    private Progress _progress;

    public SyncActivityItemViewModel(
        SyncActivityItem<long> dataItem,
        IFileSystemDisplayNameAndIconProvider fileSystemDisplayNameAndIconProvider,
        ILocalFolderService localFolderService,
        int syncPassNumber)
    {
        _dataItem = dataItem;
        _fileSystemDisplayNameAndIconProvider = fileSystemDisplayNameAndIconProvider;
        _localFolderService = localFolderService;

        OpenFolderCommand = new AsyncRelayCommand(OpenFolderAsync, CanOpenFolder);

        LastSyncPassNumber = syncPassNumber;

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

    public ImageSource? Icon
    {
        get
        {
            var name = string.IsNullOrEmpty(Name) ? "folder" : Name;

            return _dataItem.NodeType is NodeType.Directory
                ? _fileSystemDisplayNameAndIconProvider.GetFolderIconWithoutAccess(name, ShellIconSize.Small)
                : _fileSystemDisplayNameAndIconProvider.GetFileIconWithoutAccess(name, ShellIconSize.Small);
        }
    }

    public string Name => DataItem.Name;

    public Progress Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }

    public int? RootId => DataItem.RootId;

    public NodeType NodeType => DataItem.NodeType;

    public string FolderName => GetFolderDisplayName();

    public string FolderPath => GetFolderPath();

    public long? Size => DataItem.Size;

    public string? ActivityTypeDisplayText => GetActivityTypeDisplayText();

    public int LastSyncPassNumber { get; set; }

    public DateTime? SynchronizedAt
    {
        get => _synchronizedAt;
        set => SetProperty(ref _synchronizedAt, value);
    }

    public FileSystemErrorCode? ErrorCode
    {
        get => _errorCode;
        private set => SetProperty(ref _errorCode, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
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

    private static string GetResourceKeyPattern(SyncActivityItemStatus status, Replica replica)
    {
        const string type = EnumToDisplayTextConverter.TypeNamePlaceholder;
        const string value = EnumToDisplayTextConverter.ValueNamePlaceholder;

        return (status != SyncActivityItemStatus.Succeeded)
            ? $"Activity_{replica}_InProgress_{type}_Value_{value}"
            : $"Activity_{replica}_Succeeded_{type}_Value_{value}";
    }

    private static string GetErrorMessageResourceKeyPattern(Replica replica)
    {
        const string type = EnumToDisplayTextConverter.TypeNamePlaceholder;
        const string value = EnumToDisplayTextConverter.ValueNamePlaceholder;

        return $"{type}_{replica}_Value_{value}";
    }

    private static string? GetErrorMessage(FileSystemErrorCode errorCode, Replica replica, string? defaultErrorMessage)
    {
        return errorCode switch
        {
            FileSystemErrorCode.DirectoryNotFound => EnumToDisplayTextConverter.Convert(
                value: errorCode,
                parameter: GetErrorMessageResourceKeyPattern(replica)) ?? defaultErrorMessage,
            _ => defaultErrorMessage,
        };
    }

    private void OnDataItemUpdated(SyncActivityItem<long> value)
    {
        // Only some properties of data item can change
        if (value.Status is not SyncActivityItemStatus.Skipped)
        {
            Status = value.Status;
        }

        ErrorCode = value.ErrorCode;
        ErrorMessage = GetErrorMessage(value.ErrorCode, value.Replica, value.ErrorMessage);
        Progress = value.Progress;
        OnPropertyChanged(nameof(Size));
        OnPropertyChanged(nameof(RootId));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(FolderName));
        OnPropertyChanged(nameof(FolderPath));
    }

    private string? GetActivityTypeDisplayText()
    {
        var displayText = EnumToDisplayTextConverter.Convert(
            value: DataItem.ActivityType,
            parameter: GetResourceKeyPattern(Status, Replica));

        return displayText;
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

    private string GetFolderDisplayName()
    {
        var relativeFolderPath = _dataItem.RelativeParentFolderPath;

        if (!string.IsNullOrEmpty(relativeFolderPath))
        {
            return Path.GetFileName(relativeFolderPath);
        }

        // We are on the sync root folder
        return _fileSystemDisplayNameAndIconProvider.GetDisplayNameWithoutAccess(_dataItem.LocalRootPath) ?? string.Empty;
    }

    private string GetFolderPath()
    {
        var relativeFolderPath = _dataItem.RelativeParentFolderPath;

        return Path.Combine(_dataItem.LocalRootPath, relativeFolderPath);
    }
}
