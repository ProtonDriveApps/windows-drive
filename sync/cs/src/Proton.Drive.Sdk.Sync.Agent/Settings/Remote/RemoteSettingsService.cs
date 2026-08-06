using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Client;
using Proton.Drive.Sdk.Sync.Client.Core.Events;
using Proton.Drive.Sdk.Sync.Client.Settings;
using Proton.Drive.Sdk.Sync.Client.Settings.Contracts;
using Proton.Drive.Sdk.Sync.Shared.Authentication;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Reporting;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Agent.Settings.Remote;

internal sealed class RemoteSettingsService : IRemoteSettingsService, ISessionStateAware
{
    private readonly ILogger<RemoteSettingsService> _logger;
    private readonly ISettingsApiClient _settingsApiClient;
    private readonly ICoreEventProvider _coreEventProvider;
    private readonly IErrorReporting _errorReporting;
    private readonly Lazy<IEnumerable<IRemoteSettingsStateAware>> _settingsStateAwareInstances;
    private readonly Lazy<IEnumerable<IRemoteSettingsAware>> _remoteSettingsAwareInstances;

    private readonly CancellationHandle _cancellationHandle = new();
    private readonly IScheduler _scheduler;

    private RemoteSettingsStatus _status = RemoteSettingsStatus.None;
    private RemoteSettings _settings = RemoteSettings.Default;

    public RemoteSettingsService(
        ILogger<RemoteSettingsService> logger,
        ISettingsApiClient settingsApiClient,
        ICoreEventProvider coreEventProvider,
        IErrorReporting errorReporting,
        Lazy<IEnumerable<IRemoteSettingsStateAware>> settingsStateAwareInstances,
        Lazy<IEnumerable<IRemoteSettingsAware>> remoteSettingsAwareInstances)
    {
        _settingsApiClient = settingsApiClient;
        _coreEventProvider = coreEventProvider;
        _errorReporting = errorReporting;
        _settingsStateAwareInstances = settingsStateAwareInstances;
        _remoteSettingsAwareInstances = remoteSettingsAwareInstances;
        _logger = logger;

        _scheduler =
            new HandlingCancellationSchedulerDecorator(
                nameof(RemoteSettingsService),
                logger,
                new LoggingExceptionsSchedulerDecorator(
                    nameof(RemoteSettingsService),
                    logger,
                    new SerialScheduler()));

        _coreEventProvider.EventsReceived += OnCoreEventsReceived;
    }

    public Task SetUpAsync()
    {
        return Schedule(RefreshSettingsAsync);
    }

    void ISessionStateAware.OnSessionStateChanged(SessionState value)
    {
        if (value.Status is SessionStatus.Started)
        {
            Schedule(SetUpAsync);
        }
        else
        {
            _cancellationHandle.Cancel();
            Schedule(CancelSetUp);
        }
    }

    private static RemoteSettings ToRemoteSettings(GeneralSettings settings)
    {
        return new RemoteSettings
        {
            IsTelemetryEnabled = settings.IsTelemetryEnabled,
            HasInAppNotificationsEnabled = settings.News.HasFlag(NewsSettings.InAppNotificationsEnabled),
        };
    }

    private static bool HasRemoteSettingsChanged(CoreEvents events)
    {
        return events.ResumeToken.IsRefreshRequired || events.HasSettingsChanged;
    }

    private void OnCoreEventsReceived(object? sender, CoreEvents events)
    {
        if (HasRemoteSettingsChanged(events))
        {
            Schedule(RefreshSettingsAsync);
        }
    }

    private async Task SetUpAsync(CancellationToken cancellationToken)
    {
        if (_status is RemoteSettingsStatus.Succeeded)
        {
            return;
        }

        _logger.LogDebug("Remote settings set up started");

        if (await TryRefreshSettingsAsync(cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation("Remote settings set up succeeded");
        }
        else
        {
            _logger.LogWarning("Remote settings set up failed");
        }
    }

    private async Task RefreshSettingsAsync(CancellationToken cancellationToken)
    {
        if (_status is not RemoteSettingsStatus.Succeeded and not RemoteSettingsStatus.Failed)
        {
            return;
        }

        _logger.LogDebug("Remote settings refresh started");

        if (await TryRefreshSettingsAsync(cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation("Remote settings refresh succeeded");
        }
        else
        {
            _logger.LogWarning("Remote settings refresh failed");
        }
    }

    private void CancelSetUp()
    {
        if (_status is RemoteSettingsStatus.None)
        {
            return;
        }

        var settings = new GeneralSettings();
        HandleSettingsChange(settings);

        _logger.LogInformation("Remote settings set up has been cancelled");

        SetState(RemoteSettingsStatus.None);
    }

    private async Task<bool> TryRefreshSettingsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SetState(RemoteSettingsStatus.SettingUp);

        var settings = await TryGetSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (settings == null)
        {
            SetState(RemoteSettingsStatus.Failed);

            return false;
        }

        HandleSettingsChange(settings);

        SetState(RemoteSettingsStatus.Succeeded);

        return true;
    }

    private async Task<GeneralSettings?> TryGetSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settingsResponse = await _settingsApiClient.GetAsync(cancellationToken).ThrowOnFailure().ConfigureAwait(false);

            return settingsResponse.Settings;
        }
        catch (Exception ex) when (ex.IsDriveClientException())
        {
            _logger.LogWarning("Remote settings retrieval failed: {ErrorCode} {ErrorMessage}", ex.GetRelevantFormattedErrorCode(), ex.CombinedMessage());

            return null;
        }
    }

    private void HandleSettingsChange(GeneralSettings settings)
    {
        _errorReporting.IsEnabled = settings.IsSendingCrashReportsEnabled;

        var remoteSettings = ToRemoteSettings(settings);

        if (_settings == remoteSettings)
        {
            return;
        }

        _settings = remoteSettings;

        foreach (var listener in _remoteSettingsAwareInstances.Value)
        {
            listener.OnRemoteSettingsChanged(remoteSettings);
        }
    }

    private void SetState(RemoteSettingsStatus status)
    {
        _status = status;

        foreach (var listener in _settingsStateAwareInstances.Value)
        {
            listener.OnRemoteSettingsStateChanged(status);
        }
    }

    private void Schedule(Action action)
    {
        _scheduler.Schedule(action, _cancellationHandle.Token);
    }

    private Task Schedule(Func<CancellationToken, Task> action)
    {
        return _scheduler.Schedule(action, _cancellationHandle.Token);
    }
}
