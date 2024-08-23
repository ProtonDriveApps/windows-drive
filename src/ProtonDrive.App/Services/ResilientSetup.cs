using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Account;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.Devices;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Sync;
using ProtonDrive.App.Volumes;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Offline;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Shared.SyncActivity;

namespace ProtonDrive.App.Services;

internal class ResilientSetup
    : ISessionStateAware, IAccountStateAware, IVolumeStateAware, IDeviceServiceStateAware, IMappingsSetupStateAware, ISyncStateAware, IOfflineStateAware,
        IStoppableService, IDisposable
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
        IAccountService accountService,
        IVolumeService volumeService,
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
            { ServiceType.Account, new ServiceInfo(ServiceType.Account, () => accountService.SetUpAccountAsync(), "Retrying user account setup") },
            { ServiceType.Volume, new ServiceInfo(ServiceType.Volume, () => volumeService.GetActiveVolumeAsync(), "Retrying volume setup") },
            { ServiceType.Device, new ServiceInfo(ServiceType.Device, () => deviceService.SetUpDevicesAsync(), "Retrying device setup") },
            { ServiceType.MappingSetup, new ServiceInfo(ServiceType.MappingSetup, () => mappingSetupService.SetUpMappingsAsync(), "Retrying sync folder mappings setup") },
            { ServiceType.Synchronization, new ServiceInfo(ServiceType.Synchronization, () => syncService.RestartAsync(), "Restarting sync service") },
        };
    }

    private enum ServiceType
    {
        Session,
        Account,
        Volume,
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

    void IVolumeStateAware.OnVolumeStateChanged(VolumeState value)
    {
        var serviceStatus = value.Status switch
        {
            VolumeServiceStatus.Succeeded => ServiceStatus.Succeeded,
            VolumeServiceStatus.Failed => ServiceStatus.Failed,
            _ => ServiceStatus.Other,
        };

        HandleServiceStateChange(ServiceType.Volume, serviceStatus);
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
        _logger.LogInformation($"{nameof(ResilientSetup)} is stopping");
        _stopping = true;
        _handleDelay.Cancel();

        await _handleDelay.CurrentTask.ConfigureAwait(false);

        _timer.Stop();

        _logger.LogInformation($"{nameof(ResilientSetup)} stopped");
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
