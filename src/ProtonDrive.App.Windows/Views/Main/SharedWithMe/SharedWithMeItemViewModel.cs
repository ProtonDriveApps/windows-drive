using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Mapping.SyncFolders;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.App.Windows.SystemIntegration;
using ProtonDrive.App.Windows.Views.Shared;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Shared;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using SharedWithMeItem = ProtonDrive.Client.Shares.SharedWithMe.SharedWithMeItem;

namespace ProtonDrive.App.Windows.Views.Main.SharedWithMe;

internal class SharedWithMeItemViewModel : ObservableObject, IIdentifiable<string>, IMappingStatusViewModel
{
    private readonly IFileSystemDisplayNameAndIconProvider _fileSystemDisplayNameAndIconProvider;
    private readonly ILocalFolderService _localFolderService;
    private readonly IAsyncRelayCommand _toggleSyncCommand;
    private readonly IAsyncRelayCommand _removeMeCommand;
    private readonly IAsyncRelayCommand _openFolderCommand;

    private SharedWithMeItem? _dataItem;
    private SyncFolder? _syncFolder;
    private bool _isSyncEnabled;

    public SharedWithMeItemViewModel(
        IFileSystemDisplayNameAndIconProvider fileSystemDisplayNameAndIconProvider,
        ILocalFolderService localFolderService,
        IAsyncRelayCommand<SharedWithMeItemViewModel> toggleSyncCommand,
        IAsyncRelayCommand removeMeCommand)
    {
        _fileSystemDisplayNameAndIconProvider = fileSystemDisplayNameAndIconProvider;
        _localFolderService = localFolderService;
        _toggleSyncCommand = toggleSyncCommand;

        _openFolderCommand = new AsyncRelayCommand(OpenFolderAsync, CanOpenFolder);
        _removeMeCommand = removeMeCommand;
    }

    public string Id => DataItem?.Id ?? SyncFolder?.RemoteShareId ?? throw new InvalidOperationException();

    public NodeType Type => (DataItem?.IsFolder is true || SyncFolder is { Type: SyncFolderType.SharedWithMeItem, RootLinkType: LinkType.Folder })
        ? NodeType.Directory
        : NodeType.File;

    public ImageSource? Icon => Type is NodeType.Directory
        ? _fileSystemDisplayNameAndIconProvider.GetFolderIconWithoutAccess(Name, ShellIconSize.Small)
        : _fileSystemDisplayNameAndIconProvider.GetFileIconWithoutAccess(Name, ShellIconSize.Small);

    public string Name => SyncFolder?.RemoteName ?? DataItem?.Name ?? throw new InvalidOperationException();

    public string? InviterEmailAddress => DataItem?.InviterEmailAddress;

    public DateTime? SharingLocalDateTime => DataItem?.SharingTime.ToLocalTime();

    public bool IsReadOnly => DataItem?.IsReadOnly ?? SyncFolder?.RemoteIsReadOnly ?? throw new InvalidOperationException();

    public ICommand ToggleSyncCommand => _toggleSyncCommand;

    public ICommand OpenFolderCommand => _openFolderCommand;

    public ICommand RemoveMeCommand => _removeMeCommand;

    public bool IsSyncEnabled
    {
        get => _isSyncEnabled;
        private set
        {
            SetProperty(ref _isSyncEnabled, value);
        }
    }

    public MappingSetupStatus Status => _syncFolder?.Status ?? MappingSetupStatus.None;

    public MappingErrorCode ErrorCode => _syncFolder?.ErrorCode ?? MappingErrorCode.None;

    public bool SetupIsInProgress => _syncFolder?.Status is MappingSetupStatus.SettingUp;

    public MappingErrorRenderingMode RenderingMode => MappingErrorRenderingMode.Icon;

    internal SharedWithMeItem? DataItem
    {
        get => _dataItem;
        set
        {
            SetDataItem(value);

            OnPropertyChanged(nameof(Type));
            OnPropertyChanged(nameof(Icon));
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(InviterEmailAddress));
            OnPropertyChanged(nameof(SharingLocalDateTime));
            OnPropertyChanged(nameof(IsReadOnly));

            _toggleSyncCommand.NotifyCanExecuteChanged();
            _removeMeCommand.NotifyCanExecuteChanged();
        }
    }

    internal SyncFolder? SyncFolder
    {
        get => _syncFolder;
        set
        {
            _syncFolder = value;
            IsSyncEnabled = value is not null;
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(ErrorCode));

            _toggleSyncCommand.NotifyCanExecuteChanged();
            _removeMeCommand.NotifyCanExecuteChanged();
            _openFolderCommand.NotifyCanExecuteChanged();
        }
    }

    private void SetDataItem(SharedWithMeItem? value)
    {
        if (value != null && _dataItem != null && value.Id != _dataItem.Id)
        {
            throw new ArgumentException($"Cannot set {nameof(DataItem)} with a different identity value");
        }

        _dataItem = value;
    }

    private bool CanOpenFolder()
    {
        return _syncFolder is not null;
    }

    private async Task OpenFolderAsync(CancellationToken cancellationToken)
    {
        if (_syncFolder is null)
        {
            return;
        }

        await _localFolderService.OpenFolderAsync(_syncFolder.LocalPath).ConfigureAwait(true);
    }
}
