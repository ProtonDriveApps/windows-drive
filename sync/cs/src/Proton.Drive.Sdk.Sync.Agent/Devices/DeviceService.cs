using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Services;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Agent.Volumes;
using Proton.Drive.Sdk.Sync.Client;
using Proton.Drive.Sdk.Sync.Client.Devices;
using Proton.Drive.Sdk.Sync.Client.Volumes.Contracts;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Features;
using Proton.Drive.Shared.Logging;
using Proton.Drive.Shared.Offline;
using Proton.Drive.Shared.Repository;
using Proton.Drive.Shared.Threading;
using VolumeState = Proton.Drive.Sdk.Sync.Agent.Volumes.VolumeState;

namespace Proton.Drive.Sdk.Sync.Agent.Devices;

internal sealed class DeviceService : IDeviceService, IStartableService, IStoppableService, IMainVolumeStateAware, IRemoteDeviceEventsAware, IFeatureFlagsAware
{
    private readonly IDeviceClient _deviceClient;
    private readonly IRepository<DeviceSettings> _settingsRepository;
    private readonly IOfflineService _offlineService;
    private readonly Lazy<IEnumerable<IDeviceServiceStateAware>> _serviceStateAware;
    private readonly Lazy<IEnumerable<IDevicesAware>> _devicesAware;
    private readonly ILogger<DeviceService> _logger;

    private readonly CancellationHandle _cancellationHandle = new();
    private readonly IScheduler _scheduler;
    private readonly ICollection<Device> _devices = [];

    private DeviceServiceStatus _status = DeviceServiceStatus.Idle;
    private DeviceSettings _settings = new();
    private VolumeState _mainVolumeState = VolumeState.Idle;
    private volatile bool _stopping;
    private volatile bool _deviceEventHandlingDisabled;

    public DeviceService(
        IDeviceClient deviceClient,
        IRepository<DeviceSettings> settingsRepository,
        IOfflineService offlineService,
        Lazy<IEnumerable<IDeviceServiceStateAware>> serviceStateAware,
        Lazy<IEnumerable<IDevicesAware>> devicesAware,
        ILogger<DeviceService> logger)
    {
        _deviceClient = deviceClient;
        _settingsRepository = settingsRepository;
        _offlineService = offlineService;
        _serviceStateAware = serviceStateAware;
        _devicesAware = devicesAware;
        _logger = logger;

        _scheduler =
            new HandlingCancellationSchedulerDecorator(
                nameof(DeviceService),
                logger,
                new LoggingExceptionsSchedulerDecorator(
                    nameof(DeviceService),
                    logger,
                    new SerialScheduler()));
    }

    public Task SetUpDevicesAsync()
    {
        return Schedule(InternalSetUpDevicesAsync);
    }

    public Task<DeviceSetupResult> SetUpHostDeviceAsync(CancellationToken cancellationToken)
    {
        return Schedule(InternalSetUpHostDeviceAsync, DeviceSetupResult.Failure, cancellationToken);
    }

    public Task<DeviceSetupResult> GetHostDeviceAsync(CancellationToken cancellationToken)
    {
        return Schedule(InternalGetHostDeviceAsync, DeviceSetupResult.Failure, cancellationToken);
    }

    public Task RenameHostDeviceAsync(string name)
    {
        return Schedule(ct => InternalRenameHostDeviceAsync(name, ct));
    }

    async Task IStartableService.StartAsync(CancellationToken cancellationToken)
    {
        _settings = _settingsRepository.Get() ?? new DeviceSettings();

        await Schedule(() => GetOrCreateHostDevice()).ConfigureAwait(false);

        _logger.LogInformation($"{nameof(DeviceService)} started");
    }

    async Task IStoppableService.StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{nameof(DeviceService)} is stopping");
        _stopping = true;
        _cancellationHandle.Cancel();

        await WaitForCompletionAsync().ConfigureAwait(false);

        _logger.LogInformation($"{nameof(DeviceService)} stopped");
    }

    void IMainVolumeStateAware.OnMainVolumeStateChanged(VolumeState value)
    {
        _mainVolumeState = value;

        if (value.Status is VolumeStatus.Ready)
        {
            _logger.LogDebug("Scheduling devices set up");
            Schedule(InternalSetUpDevicesAsync);
        }
        else
        {
            if (_status is not DeviceServiceStatus.Idle)
            {
                _logger.LogDebug("Scheduling cancellation of devices setup");
            }

            _cancellationHandle.Cancel();
            Schedule(CancelSetupAsync);
        }
    }

    void IRemoteDeviceEventsAware.OnDeviceCreated(string deviceLinkId)
    {
        if (_deviceEventHandlingDisabled)
        {
            return;
        }

        _logger.LogInformation("Remote event indicates that a new device could have been created. Scheduling refresh...");

        _ = RefreshDevicesAsync();
    }

    void IRemoteDeviceEventsAware.OnDeviceDeleted(string deviceLinkId)
    {
        if (_deviceEventHandlingDisabled)
        {
            return;
        }

        var deletedDevice = _devices.FirstOrDefault(x => x.LinkId == deviceLinkId);

        if (deletedDevice is null)
        {
            return;
        }

        _logger.LogInformation(
            "Remote event indicates that {DeviceType} device with LinkID=\"{LinkId}\" was deleted. Scheduling refresh...",
            deletedDevice.Type,
            deviceLinkId);

        _ = RefreshDevicesAsync();
    }

    void IFeatureFlagsAware.OnFeatureFlagsChanged(IReadOnlyDictionary<Feature, bool> features)
    {
        _deviceEventHandlingDisabled = features[Feature.DriveWindowsDeviceEventHandlingDisabled];
    }

    internal Task WaitForCompletionAsync()
    {
        // Wait for all scheduled tasks to complete
        return _scheduler.Schedule(() => { });
    }

    private static Proton.Drive.Sdk.Sync.Client.Devices.Device CreateDevice(string name, string? id) =>
        new()
        {
            Id = id ?? string.Empty,
            Name = name,
            Platform = DevicePlatform.Windows,
            IsSynchronizationEnabled = true,
        };

    private Task InternalSetUpDevicesAsync(CancellationToken cancellationToken)
    {
        if (_status is DeviceServiceStatus.Succeeded)
        {
            return Task.CompletedTask;
        }

        return InternalRefreshAndSetStatusAsync(cancellationToken);
    }

    private async Task InternalRefreshAndSetStatusAsync(CancellationToken cancellationToken)
    {
        if (_mainVolumeState.Status is not VolumeStatus.Ready)
        {
            return;
        }

        SetStatus(DeviceServiceStatus.SettingUp);

        var succeeded = await TryRefreshDevicesAsync(cancellationToken).ConfigureAwait(false);

        SetStatus(succeeded ? DeviceServiceStatus.Succeeded : DeviceServiceStatus.Failed);
    }

    private Task CancelSetupAsync(CancellationToken cancellationToken)
    {
        if (_status is not DeviceServiceStatus.Idle)
        {
            _logger.LogInformation("Devices setup has been cancelled");
        }

        SetStatus(DeviceServiceStatus.SettingUp);

        ResetHostDevice();
        ClearForeignDevices();

        SetStatus(DeviceServiceStatus.Idle);

        return Task.CompletedTask;
    }

    private async Task<bool> TryRefreshDevicesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await InternalRefreshDevicesAsync(cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (Exception ex) when (ex.IsDriveClientException())
        {
            _logger.LogError("Failed to get remote devices: {Message}", ex.CombinedMessage());

            return false;
        }
    }

    private async Task InternalRefreshDevicesAsync(CancellationToken cancellationToken)
    {
        var clientDevices = await _deviceClient.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var unprocessedIds = _devices.Select(d => d.Id).ToHashSet();

        foreach (var clientDevice in clientDevices)
        {
            var isHostDevice = clientDevice.Id == (_settings.DeviceId ?? string.Empty);
            AddOrUpdateDevice(clientDevice, isHostDevice ? DeviceType.Host : DeviceType.Foreign);
            unprocessedIds.Remove(clientDevice.Id);
        }

        foreach (var unprocessedId in unprocessedIds)
        {
            var unprocessedDevice = _devices.FirstOrDefault(d => d.Id == unprocessedId);
            if (unprocessedDevice is null)
            {
                continue;
            }

            if (unprocessedDevice.Type is DeviceType.Host)
            {
                // The host device does not exist on remote, clear and persist device ID value in settings
                SaveHostDevice(ResetAndGetHostDevice());
            }
            else
            {
                RemoveDevice(unprocessedDevice);
            }
        }
    }

    private async Task<DeviceSetupResult> InternalSetUpHostDeviceAsync(CancellationToken cancellationToken)
    {
        if (TryGetRemoteHostDevice(out var hostDevice))
        {
            return new DeviceSetupResult(hostDevice);
        }

        if (_status is not DeviceServiceStatus.Succeeded)
        {
            _logger.LogWarning("Device service status is {Status}", _status);
            return DeviceSetupResult.Failure;
        }

        var volumeState = _mainVolumeState;
        if (volumeState.Status is not VolumeStatus.Ready || volumeState.Volume is null)
        {
            _logger.LogWarning("Remote {Type} volume is not available", VolumeType.Main);
            return DeviceSetupResult.Failure;
        }

        var name = GetDeviceName();
        var nameToLog = _logger.GetSensitiveValueForLogging(name);

        try
        {
            var clientDevice = await _deviceClient.CreateAsync(volumeState.Volume.Id, name, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Created remote device \"{Name}\" with ID={DeviceId}, Share ID={ShareId}, Link ID={LinkId}",
                nameToLog,
                clientDevice.Id,
                clientDevice.ShareId,
                clientDevice.LinkId);

            var device = AddOrUpdateDevice(clientDevice, DeviceType.Host);
            return new DeviceSetupResult(device);
        }
        catch (Exception ex) when (ex.IsDriveClientException())
        {
            _logger.LogError("Failed to create remote device \"{Name}\": {ErrorMessage}", nameToLog, ex.CombinedMessage());

            return ex is ApiException apiException
                ? new DeviceSetupResult(null, ErrorCode: apiException.ResponseCode)
                : DeviceSetupResult.Failure;
        }
    }

    private Task<DeviceSetupResult> InternalGetHostDeviceAsync(CancellationToken cancellationToken)
    {
        if (!TryGetRemoteHostDevice(out var hostDevice))
        {
            if (_status is not DeviceServiceStatus.Succeeded)
            {
                _logger.LogWarning("Device service status is {Status}", _status);
                return Task.FromResult(DeviceSetupResult.Failure);
            }

            _logger.LogWarning("Host device not found");
            return Task.FromResult(DeviceSetupResult.DeviceNotFound);
        }

        return Task.FromResult(new DeviceSetupResult(hostDevice));
    }

    private async Task InternalRenameHostDeviceAsync(string name, CancellationToken cancellationToken)
    {
        if (!TryGetRemoteHostDevice(out var hostDevice))
        {
            _logger.LogWarning("Failed to rename host device: remote device not created");
            return;
        }

        ForceOnline();

        var nameToLog = _logger.GetSensitiveValueForLogging(name);

        try
        {
            var renamedClientDevice = await _deviceClient.RenameAsync(hostDevice.DataItem, name, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Renamed remote device with ID={DeviceId} to \"{Name}\"", hostDevice.Id, nameToLog);

            AddOrUpdateDevice(renamedClientDevice, DeviceType.Host);
        }
        catch (Exception ex) when (ex.IsDriveClientException())
        {
            _logger.LogError("Failed to rename remote device with ID={DeviceId}: {ErrorMessage}", hostDevice.Id, ex.CombinedMessage());
        }
    }

    private bool TryGetRemoteHostDevice([MaybeNullWhen(false)] out Device hostDevice)
    {
        hostDevice = GetOrCreateHostDevice();

        // An empty device ID value indicates the remote device does not exist or is not yet retrieved
        return !string.IsNullOrEmpty(hostDevice.Id);
    }

    private Device GetOrCreateHostDevice()
    {
        return _devices.FirstOrDefault(d => d.Type is DeviceType.Host) ?? ResetAndGetHostDevice();
    }

    private Device ResetAndGetHostDevice()
    {
        ResetHostDevice();

        return _devices.First(d => d.Type is DeviceType.Host);
    }

    private void ResetHostDevice()
    {
        AddOrUpdateDevice(CreateDevice(GetDeviceName(), id: null), DeviceType.Host);
    }

    private string GetDeviceName()
    {
        return !string.IsNullOrEmpty(_settings.DeviceName) ? _settings.DeviceName : Environment.MachineName;
    }

    private void ClearForeignDevices()
    {
        foreach (var device in _devices.Where(d => d.Type is DeviceType.Foreign).ToList())
        {
            RemoveDevice(device);
        }
    }

    private Device AddOrUpdateDevice(Proton.Drive.Sdk.Sync.Client.Devices.Device clientDevice, DeviceType type)
    {
        // Only a single host device should exist
        var device = _devices.FirstOrDefault(d => d.Id == clientDevice.Id || (type is DeviceType.Host && d.Type is DeviceType.Host));

        if (device == null)
        {
            device = new Device(type, clientDevice);

            AddDevice(device);
        }
        else
        {
            if (type != device.Type)
            {
                throw new InvalidOperationException("Device type cannot change");
            }

            UpdateDevice(device, clientDevice);
        }

        if (type is not DeviceType.Host)
        {
            return device;
        }

        var hostDeviceHasChanged = (_settings.DeviceId ?? string.Empty) != device.Id || _settings.DeviceName != device.Name;

        if (hostDeviceHasChanged && !string.IsNullOrEmpty(device.Id))
        {
            SaveHostDevice(device);
        }

        return device;
    }

    private void SaveHostDevice(Device hostDevice)
    {
        _settings.DeviceId = string.IsNullOrEmpty(hostDevice.Id) ? null : hostDevice.Id;
        _settings.DeviceName = hostDevice.Name;
        _settingsRepository.Set(_settings);
    }

    private Task RefreshDevicesAsync()
    {
        return Schedule(InternalRefreshAndSetStatusAsync);
    }

    private void AddDevice(Device device)
    {
        _devices.Add(device);
        OnDeviceChanged(DeviceChangeType.Added, device);
    }

    private void UpdateDevice(Device device, Proton.Drive.Sdk.Sync.Client.Devices.Device clientDevice)
    {
        if (device.Update(clientDevice))
        {
            OnDeviceChanged(DeviceChangeType.Updated, device);
        }
    }

    private void RemoveDevice(Device device)
    {
        _devices.Remove(device);
        OnDeviceChanged(DeviceChangeType.Removed, device);
    }

    private void ForceOnline()
    {
        if (_stopping)
        {
            return;
        }

        _offlineService.ForceOnline();
    }

    private void OnDeviceChanged(DeviceChangeType changeType, Device device)
    {
        _logger.LogInformation("Cached {DeviceType} device {ChangeType} with ID \"{Id}\"", device.Type, changeType, device.Id);

        foreach (var listener in _devicesAware.Value)
        {
            listener.OnDeviceChanged(changeType, device);
        }
    }

    private void SetStatus(DeviceServiceStatus value)
    {
        _status = value;
        OnStateChanged(value);
    }

    private void OnStateChanged(DeviceServiceStatus value)
    {
        _logger.LogInformation("Device service state changed to {Status}", value);

        foreach (var listener in _serviceStateAware.Value)
        {
            listener.OnDeviceServiceStateChanged(value);
        }
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    private Task Schedule(Action action)
    {
        return _scheduler.Schedule(action, _cancellationHandle.Token);
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    private Task Schedule(Func<CancellationToken, Task> action)
    {
        if (_stopping)
        {
            return Task.CompletedTask;
        }

        var cancellationToken = _cancellationHandle.Token;

        return _scheduler.Schedule(() => action(cancellationToken), cancellationToken);
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    private async Task<TResult> Schedule<TResult>(Func<CancellationToken, Task<TResult>> function, TResult defaultResult, CancellationToken cancellationToken)
    {
        if (_stopping)
        {
            return defaultResult;
        }

        return await _scheduler.Schedule(() => function(cancellationToken), cancellationToken).ConfigureAwait(false);
    }
}
