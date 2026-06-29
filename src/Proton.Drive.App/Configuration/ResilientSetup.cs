using Microsoft.Extensions.Logging;
using Proton.Drive.App.Photos.Volume;
using Proton.Drive.Sdk.Sync.Agent.Services;
using Proton.Drive.Sdk.Sync.Agent.Volumes;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Offline;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.App.Configuration;

internal sealed class ResilientSetup : IPhotoVolumeStateAware, IOfflineStateAware, IStoppableService, IDisposable
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
        IPhotoVolumeService photoVolumeService,
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
            { ServiceType.PhotoVolume, new ServiceInfo(ServiceType.PhotoVolume, () => photoVolumeService.RetryFailedSetupAsync(), "Retrying photo volume setup") },
        };
    }

    private enum ServiceType
    {
        PhotoVolume,
    }

    private enum ServiceStatus
    {
        Other,
        Succeeded,
        Failed,
    }

    void IPhotoVolumeStateAware.OnPhotoVolumeStateChanged(VolumeState value)
    {
        var serviceStatus = value.Status switch
        {
            VolumeStatus.Ready => ServiceStatus.Succeeded,
            VolumeStatus.Failed => ServiceStatus.Failed,
            _ => ServiceStatus.Other,
        };

        HandleServiceStateChange(ServiceType.PhotoVolume, serviceStatus);
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
