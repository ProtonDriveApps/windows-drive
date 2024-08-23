using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ProtonDrive.App.Devices;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Mapping.SyncFolders;
using ProtonDrive.App.Settings;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.App.Windows.Extensions;
using ProtonDrive.App.Windows.Services;
using ProtonDrive.App.Windows.SystemIntegration;
using ProtonDrive.App.Windows.Toolkit.Threading;

namespace ProtonDrive.App.Windows.Views.Main.Computers;

internal sealed class SyncedDevicesViewModel : PageViewModel, IDeviceServiceStateAware, IDevicesAware, ISyncFoldersAware, IMappingStateAware
{
    private readonly IDeviceService _deviceService;
    private readonly IFileSystemDisplayNameAndIconProvider _fileSystemDisplayNameAndIconProvider;
    private readonly ISyncFolderService _syncFolderService;
    private readonly ILocalFolderService _localFolderService;
    private readonly IDialogService _dialogService;
    private readonly Func<AddFoldersViewModel> _addFolderViewModelFactory;
    private readonly DispatcherScheduler _scheduler;
    private readonly RelayCommand _editDeviceNameCommand;

    private bool _areDevicesAvailable;
    private DeviceViewModel? _hostDevice;
    private bool _isEditing;
    private string? _newDeviceName;
    private bool _isNewDeviceNameValid = true;

    public SyncedDevicesViewModel(
        IDeviceService deviceService,
        IFileSystemDisplayNameAndIconProvider fileSystemDisplayNameAndIconProvider,
        ISyncFolderService syncFolderService,
        ILocalFolderService localFolderService,
        IDialogService dialogService,
        Func<AddFoldersViewModel> addFolderViewModelFactory,
        DispatcherScheduler scheduler)
    {
        _deviceService = deviceService;
        _fileSystemDisplayNameAndIconProvider = fileSystemDisplayNameAndIconProvider;
        _syncFolderService = syncFolderService;
        _localFolderService = localFolderService;
        _dialogService = dialogService;
        _addFolderViewModelFactory = addFolderViewModelFactory;
        _scheduler = scheduler;

        AddFoldersCommand = new RelayCommand(AddFolders);
        _editDeviceNameCommand = new RelayCommand(EditDeviceName, CanEditDeviceName);
        CancelDeviceNameCommand = new RelayCommand(CancelDeviceNameEditing);
        SaveDeviceNameCommand = new AsyncRelayCommand(SaveDeviceNameAsync, CanSaveDeviceName);
    }

    public string? NewDeviceName
    {
        get => _newDeviceName;
        set
        {
            if (SetProperty(ref _newDeviceName, value))
            {
                IsNewDeviceNameValid = true;
            }
        }
    }

    public bool IsNewDeviceNameValid
    {
        get => _isNewDeviceNameValid;
        private set => SetProperty(ref _isNewDeviceNameValid, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (SetProperty(ref _isEditing, value))
            {
                SaveDeviceNameCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool AreDevicesAvailable
    {
        get => _areDevicesAvailable;
        private set => SetProperty(ref _areDevicesAvailable, value);
    }

    public DeviceViewModel? HostDevice
    {
        get => _hostDevice;
        private set => SetProperty(ref _hostDevice, value);
    }

    public ObservableCollection<DeviceViewModel> ForeignDevices { get; } = new();
    public ObservableCollection<SyncedFolderViewModel> SyncedFolders { get; } = new();

    public ICommand AddFoldersCommand { get; }
    public ICommand EditDeviceNameCommand => _editDeviceNameCommand;
    public AsyncRelayCommand SaveDeviceNameCommand { get; }
    public ICommand CancelDeviceNameCommand { get; }

    void IDeviceServiceStateAware.OnDeviceServiceStateChanged(DeviceServiceStatus status)
    {
        AreDevicesAvailable = status is DeviceServiceStatus.Succeeded;

        if (status is not DeviceServiceStatus.Succeeded)
        {
            CancelDeviceNameEditing();
        }
    }

    void IDevicesAware.OnDeviceChanged(DeviceChangeType changeType, Device device)
    {
        switch (device.Type)
        {
            case DeviceType.Host:
                HandleHostDeviceChange(changeType, device);
                break;
            case DeviceType.Foreign:
                HandleForeignDeviceChange(changeType, device);
                break;
            default:
                throw new ArgumentException("Device type is out of range");
        }
    }

    void ISyncFoldersAware.OnSyncFolderChanged(SyncFolderChangeType changeType, SyncFolder folder)
    {
        if (folder.Type is not SyncFolderType.HostDeviceFolder)
        {
            return;
        }

        switch (changeType)
        {
            case SyncFolderChangeType.Added:

                if (!_fileSystemDisplayNameAndIconProvider.TryGetDisplayNameAndIcon(folder.LocalPath, ShellIconSize.Small, out var name, out var icon))
                {
                    name = Path.GetFileName(folder.LocalPath);
                    icon = _fileSystemDisplayNameAndIconProvider.GetFolderIconWithoutAccess(folder.LocalPath, ShellIconSize.Small);
                }

                Schedule(() => SyncedFolders.Add(new SyncedFolderViewModel(folder, name, icon, _localFolderService, _dialogService, RemoveSyncFolderAsync)));
                break;

            case SyncFolderChangeType.Updated:
                Schedule(
                    () =>
                    {
                        var syncedFolderToUpdate = SyncedFolders.FirstOrDefault(syncedFolder => syncedFolder.Equals(folder));

                        if (syncedFolderToUpdate is null)
                        {
                            return;
                        }

                        syncedFolderToUpdate.ErrorCode = folder.ErrorCode;
                        syncedFolderToUpdate.Status = folder.Status;
                    });
                break;

            case SyncFolderChangeType.Removed:
                Schedule(() => SyncedFolders.RemoveFirst(syncedFolder => syncedFolder.Equals(folder)));
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(changeType), changeType, null);
        }
    }

    void IMappingStateAware.OnMappingStateChanged(RemoteToLocalMapping mapping, MappingState state)
    {
        if (mapping.Type is not MappingType.ForeignDevice)
        {
            return;
        }

        Schedule(UpdateMappingStatus);

        void UpdateMappingStatus()
        {
            var foreignDevice = ForeignDevices.FirstOrDefault(x => x.VolumeId == mapping.Remote.VolumeId && x.ShareId == mapping.Remote.ShareId);

            if (foreignDevice is null)
            {
                return;
            }

            foreignDevice.SetupErrorCode = state.ErrorCode;
            foreignDevice.SetupStatus = state.Status;
        }
    }

    private bool CanEditDeviceName() => HostDevice?.ExistsOnRemote == true;

    private void EditDeviceName()
    {
        NewDeviceName = _hostDevice?.Name;
        IsEditing = true;
    }

    private void CancelDeviceNameEditing()
    {
        IsEditing = false;
    }

    private bool CanSaveDeviceName() => IsEditing;

    private async Task SaveDeviceNameAsync()
    {
        if (string.IsNullOrWhiteSpace(NewDeviceName))
        {
            IsNewDeviceNameValid = false;
            return;
        }

        if (HostDevice is not null)
        {
            await _deviceService.RenameHostDeviceAsync(NewDeviceName).ConfigureAwait(true);
        }

        IsEditing = false;
    }

    private void HandleHostDeviceChange(DeviceChangeType changeType, Device device)
    {
        switch (changeType)
        {
            case DeviceChangeType.Added:
                HostDevice = new DeviceViewModel(device);
                break;

            case DeviceChangeType.Updated:
                HostDevice?.DataItemUpdated();
                break;

            case DeviceChangeType.Removed:
                HostDevice = null;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(changeType), changeType, null);
        }

        Schedule(() => _editDeviceNameCommand.NotifyCanExecuteChanged());
    }

    private void HandleForeignDeviceChange(DeviceChangeType changeType, Device device)
    {
        switch (changeType)
        {
            case DeviceChangeType.Added:
                Schedule(() => ForeignDevices.Add(new DeviceViewModel(device)));
                break;

            case DeviceChangeType.Updated:
                Schedule(() => ForeignDevices.FirstOrDefault(d => d.Equals(device))?.DataItemUpdated());
                break;

            case DeviceChangeType.Removed:
                Schedule(() => ForeignDevices.RemoveFirst(d => d.Equals(device)));
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(changeType), changeType, null);
        }
    }

    private void AddFolders()
    {
        var dialogViewModel = _addFolderViewModelFactory.Invoke();
        dialogViewModel.RefreshSyncedFolders(SyncedFolders.Select(x => x.Path).ToHashSet());
        _dialogService.ShowDialog(dialogViewModel);
    }

    private async Task RemoveSyncFolderAsync(SyncFolder syncFolder, CancellationToken cancellationToken)
    {
        await _syncFolderService.RemoveHostDeviceFolderAsync(syncFolder, cancellationToken).ConfigureAwait(true);
    }

    private void Schedule(Action action)
    {
        _scheduler.Schedule(action);
    }
}
