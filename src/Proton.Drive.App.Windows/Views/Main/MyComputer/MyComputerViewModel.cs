using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Proton.Drive.App.Onboarding;
using Proton.Drive.App.Windows.Extensions;
using Proton.Drive.Sdk.Sync.Agent.Devices;
using Proton.Drive.Sdk.Sync.Agent.Mapping;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Shared.Features;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.App.Windows.Views.Main.MyComputer;

internal sealed class MyComputerViewModel
    : PageViewModel, IDeviceServiceStateAware, IDevicesAware, IMappingStateAware, IStorageOptimizationOnboardingStateAware, IFeatureFlagsAware
{
    private readonly IDeviceService _deviceService;
    private readonly IOnboardingService _onboardingService;
    private readonly IScheduler _scheduler;

    private bool _areDevicesAvailable;
    private DeviceViewModel? _hostDevice;
    private bool _isEditing;
    private string? _newDeviceName;
    private bool _isNewDeviceNameValid = true;
    private StorageOptimizationOnboardingStep _onboardingStep;
    private bool _isOnboarding;
    private bool _isStorageOptimizationFeatureEnabled = true;

    public MyComputerViewModel(
        IDeviceService deviceService,
        FolderListViewModel folderListViewModel,
        IOnboardingService onboardingService,
        [FromKeyedServices("Dispatcher")] IScheduler scheduler)
    {
        _deviceService = deviceService;
        _onboardingService = onboardingService;
        _scheduler = scheduler;

        Folders = folderListViewModel;

        NextOnboardingPageCommand = new RelayCommand(DisplayNextOnboardingPage);
        GetStartedCommand = new RelayCommand(EndOnboarding);
        DismissOnboardingCommand = new RelayCommand(EndOnboarding);

        EditDeviceNameCommand = new RelayCommand(EditDeviceName, CanEditDeviceName);
        CancelDeviceNameCommand = new RelayCommand(CancelDeviceNameEditing);
        SaveDeviceNameCommand = new AsyncRelayCommand(SaveDeviceNameAsync, CanSaveDeviceName);
    }

    public bool IsOnboarding
    {
        get => _isOnboarding;
        private set => SetProperty(ref _isOnboarding, value);
    }

    public StorageOptimizationOnboardingStep OnboardingStep
    {
        get => _onboardingStep;
        private set => SetProperty(ref _onboardingStep, value);
    }

    public ICommand NextOnboardingPageCommand { get; }

    public ICommand GetStartedCommand { get; }

    public ICommand DismissOnboardingCommand { get; }

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

    public FolderListViewModel Folders { get; }
    public ObservableCollection<DeviceViewModel> ForeignDevices { get; } = [];

    public IRelayCommand EditDeviceNameCommand { get; }
    public IAsyncRelayCommand SaveDeviceNameCommand { get; }
    public ICommand CancelDeviceNameCommand { get; }

    void IDeviceServiceStateAware.OnDeviceServiceStateChanged(DeviceServiceStatus status)
    {
        AreDevicesAvailable = status is DeviceServiceStatus.Succeeded;

        if (status is not DeviceServiceStatus.Succeeded)
        {
            Schedule(CancelDeviceNameEditing);
        }
    }

    void IDevicesAware.OnDeviceChanged(DeviceChangeType changeType, Device device)
    {
        Schedule(HandleDeviceChange);

        return;

        void HandleDeviceChange()
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
    }

    void IMappingStateAware.OnMappingStateChanged(RemoteToLocalMapping mapping, MappingState state)
    {
        if (mapping.Type is not MappingType.ForeignDevice)
        {
            return;
        }

        Schedule(UpdateMappingStatus);

        return;

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

    void IStorageOptimizationOnboardingStateAware.StorageOptimizationOnboardingStateChanged(StorageOptimizationOnboardingStep value)
    {
        OnboardingStep = value;
        Schedule(RefreshOnboarding);
    }

    void IFeatureFlagsAware.OnFeatureFlagsChanged(IReadOnlyDictionary<Feature, bool> features)
    {
        _isStorageOptimizationFeatureEnabled = !features[Feature.DriveWindowsStorageOptimizationDisabled];
        RefreshOnboarding();
    }

    private void DisplayNextOnboardingPage()
    {
        _onboardingService.CompleteStorageOptimizationOnboardingStep(OnboardingStep);
    }

    private void EndOnboarding()
    {
        _onboardingService.CompleteStorageOptimizationOnboardingStep(StorageOptimizationOnboardingStep.Completed);
    }

    private void RefreshOnboarding()
    {
        IsOnboarding = _isStorageOptimizationFeatureEnabled && OnboardingStep is not StorageOptimizationOnboardingStep.None;
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

        EditDeviceNameCommand.NotifyCanExecuteChanged();
    }

    private void HandleForeignDeviceChange(DeviceChangeType changeType, Device device)
    {
        switch (changeType)
        {
            case DeviceChangeType.Added:
                ForeignDevices.Add(new DeviceViewModel(device));
                break;

            case DeviceChangeType.Updated:
                ForeignDevices.FirstOrDefault(d => d.Equals(device))?.DataItemUpdated();
                break;

            case DeviceChangeType.Removed:
                ForeignDevices.RemoveFirst(d => d.Equals(device));
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(changeType), changeType, null);
        }
    }

    private void Schedule(Action action)
    {
        _scheduler.Schedule(action);
    }
}
