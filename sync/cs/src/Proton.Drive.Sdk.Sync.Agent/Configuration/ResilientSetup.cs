using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Account;
using Proton.Drive.Sdk.Sync.Agent.Devices;
using Proton.Drive.Sdk.Sync.Agent.Mapping;
using Proton.Drive.Sdk.Sync.Agent.Services;
using Proton.Drive.Sdk.Sync.Agent.Settings.Remote;
using Proton.Drive.Sdk.Sync.Agent.Volumes;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.Authentication;
using Proton.Drive.Sdk.Sync.Shared.SyncActivity;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Offline;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Agent.Configuration;

internal class ResilientSetup :
    ISessionStateAware, IRemoteSettingsStateAware, IAccountStateAware, IMainVolumeStateAware, IDeviceServiceStateAware,
    IMappingsSetupStateAware, ISyncStateAware, IOfflineStateAware, IStoppableService, IDisposable
{
    private readonly ILogger<ResilientSetup> _logger;

    private readonly ISchedulerTimer _timer;
    private readonly CoalescingAction _handleDelay;
    private readonly IReadOnlyDictionary<ServiceType, ServiceInfo> _services;
    private readonly IRateLimiter<ServiceType> _retryRateLimiter;

    private volatile bool _stopping;
    private bool _isOffline;

    public ResilientSetup(
        AppConfig appConfig,
        IScheduler scheduler,
        IClock clock,
        IStatefulSessionService sessionService,
        IRemoteSettingsService settingsService,
        IAccountService accountService,
        IMainVolumeService mainVolumeService,
        IDeviceService deviceService,
        IMappingSetupService mappingSetupService,
        ISyncService syncService,
        ILogger<ResilientSetup> logger)
    {
        _logger = logger;

        _retryRateLimiter = new RateLimiter<ServiceType>(clock, appConfig.MinFailedSetupRetryInterval, appConfig.MaxFailedSetupRetryInterval);

        _timer = scheduler.CreateTimer();
        _timer.Interval = (appConfig.MinFailedSetupRetryInterval / 2.5).RandomizedWithDeviation(0.2);
        _timer.Tick += OnTimerTick;

        _handleDelay = new CoalescingAction(HandleDelay);

        _services = new Dictionary<ServiceType, ServiceInfo>
        {
            { ServiceType.Session, new ServiceInfo(ServiceType.Session, () => sessionService.StartSessionAsync(), "Retrying session start") },
            { ServiceType.RemoteSettings, new ServiceInfo(ServiceType.RemoteSettings, () => settingsService.SetUpAsync(), "Retrying remote settings setup") },
            { ServiceType.Account, new ServiceInfo(ServiceType.Account, () => accountService.SetUpAccountAsync(), "Retrying user account setup") },
            { ServiceType.MainVolume, new ServiceInfo(ServiceType.MainVolume, () => mainVolumeService.GetVolumeAsync(), "Retrying main volume setup") },
            { ServiceType.Device, new ServiceInfo(ServiceType.Device, () => deviceService.SetUpDevicesAsync(), "Retrying device setup") },
            { ServiceType.MappingSetup, new ServiceInfo(ServiceType.MappingSetup, () => mappingSetupService.SetUpMappingsAsync(), "Retrying sync folder mappings setup") },
            { ServiceType.Synchronization, new ServiceInfo(ServiceType.Synchronization, () => syncService.RestartAsync(), "Restarting sync service") },
        };
    }

    private enum ServiceType
    {
        Session,
        RemoteSettings,
        Account,
        MainVolume,
        Device,
        MappingSetup,
        Synchronization,
    }

    private enum ServiceStatus
    {
        Other,
        Succeeded,
        Failed,
    }

    void ISessionStateAware.OnSessionStateChanged(SessionState value)
    {
        var serviceStatus = value.Status switch
        {
            SessionStatus.Started => ServiceStatus.Succeeded,
            SessionStatus.Failed => ServiceStatus.Failed,
            _ => ServiceStatus.Other,
        };

        HandleServiceStateChange(ServiceType.Session, serviceStatus);
    }

    void IRemoteSettingsStateAware.OnRemoteSettingsStateChanged(RemoteSettingsStatus status)
    {
        var serviceStatus = status switch
        {
            RemoteSettingsStatus.Succeeded => ServiceStatus.Succeeded,
            RemoteSettingsStatus.Failed => ServiceStatus.Failed,
            _ => ServiceStatus.Other,
        };

        HandleServiceStateChange(ServiceType.RemoteSettings, serviceStatus);
    }

    void IAccountStateAware.OnAccountStateChanged(AccountState value)
    {
        var serviceStatus = value.Status switch
        {
            AccountStatus.Succeeded => ServiceStatus.Succeeded,
            AccountStatus.Failed => ServiceStatus.Failed,
            _ => ServiceStatus.Other,
        };

        HandleServiceStateChange(ServiceType.Account, serviceStatus);
    }

    void IMainVolumeStateAware.OnMainVolumeStateChanged(VolumeState value)
    {
        var serviceStatus = value.Status switch
        {
            VolumeStatus.Ready => ServiceStatus.Succeeded,
            VolumeStatus.Failed => ServiceStatus.Failed,
            _ => ServiceStatus.Other,
        };

        HandleServiceStateChange(ServiceType.MainVolume, serviceStatus);
    }

    void IDeviceServiceStateAware.OnDeviceServiceStateChanged(DeviceServiceStatus status)
    {
        var serviceStatus = status switch
        {
            DeviceServiceStatus.Succeeded => ServiceStatus.Succeeded,
            DeviceServiceStatus.Failed => ServiceStatus.Failed,
            _ => ServiceStatus.Other,
        };

        HandleServiceStateChange(ServiceType.Device, serviceStatus);
    }

    void IMappingsSetupStateAware.OnMappingsSetupStateChanged(MappingsSetupState value)
    {
        var serviceStatus = value.Status switch
        {
            MappingSetupStatus.Succeeded => ServiceStatus.Succeeded,
            MappingSetupStatus.PartiallySucceeded => ServiceStatus.Failed,
            MappingSetupStatus.Failed => ServiceStatus.Failed,
            _ => ServiceStatus.Other,
        };

        HandleServiceStateChange(ServiceType.MappingSetup, serviceStatus);
    }

    void ISyncStateAware.OnSyncStateChanged(SyncState value)
    {
        var serviceStatus = value.Status switch
        {
            SyncStatus.Idle => ServiceStatus.Succeeded,
            SyncStatus.Failed => ServiceStatus.Failed,
            _ => ServiceStatus.Other,
        };

        HandleServiceStateChange(ServiceType.Synchronization, serviceStatus);
    }

    void IOfflineStateAware.OnOfflineStateChanged(OfflineStatus status)
    {
        _isOffline = status == OfflineStatus.Offline;

        if (status == OfflineStatus.Testing && !_timer.IsEnabled)
        {
            // Retry failed set up immediately when back online
            // and not reset manually from Offline straight to Online.
            RetryIfNeeded();
        }

        HandleRetry();
    }

    async Task IStoppableService.StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug($"{nameof(ResilientSetup)} is stopping");
        _stopping = true;
        _handleDelay.Cancel();

        await _handleDelay.WaitForCompletionAsync().ConfigureAwait(false);

        _timer.Stop();

        _logger.LogDebug($"{nameof(ResilientSetup)} stopped");
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    private void HandleServiceStateChange(ServiceType serviceType, ServiceStatus status)
    {
        _services[serviceType].HasFailed = status is ServiceStatus.Failed;

        if (status is ServiceStatus.Failed)
        {
            _retryRateLimiter.DecreaseRate(serviceType);

            HandleRetry();
        }
        else if (status is ServiceStatus.Succeeded)
        {
            _retryRateLimiter.ResetRate(serviceType);
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        RetryIfNeeded();
    }

    private void RetryIfNeeded()
    {
        if (_stopping || _isOffline)
        {
            return;
        }

        foreach (var service in _services.Values.Where(s => s.HasFailed))
        {
            if (!_retryRateLimiter.CanExecute(service.Type))
            {
                continue;
            }

            _logger.LogDebug(service.RestartMessage);
            service.RestartAction.Invoke();

            return;
        }

        HandleRetry();
    }

    private void HandleRetry()
    {
        if (_stopping)
        {
            return;
        }

        _handleDelay.Run();
    }

    private void HandleDelay()
    {
        if (_stopping)
        {
            return;
        }

        if (!_isOffline && _services.Values.Any(s => s.HasFailed))
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private class ServiceInfo
    {
        public ServiceInfo(ServiceType type, Action restartAction, string restartMessage)
        {
            Type = type;
            RestartAction = restartAction;
            RestartMessage = restartMessage;
        }

        public ServiceType Type { get; }
        public Action RestartAction { get; }
        public string RestartMessage { get; }

        public bool HasFailed { get; set; }
    }
}
