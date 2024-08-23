using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Account;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.Reporting;
using ProtonDrive.Client;
using ProtonDrive.Client.Settings;
using ProtonDrive.Client.Settings.Contracts;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.App.Settings.Remote;

public sealed class RemoteSettingsService : ISessionStateAware
{
    private readonly ISettingsApiClient _settingsApiClient;
    private readonly IErrorReporting _errorReporting;
    private readonly Lazy<IEnumerable<IRemoteSettingsStateAware>> _remoteSettingsStateAware;
    private readonly ILogger<RemoteSettingsService> _logger;

    private readonly CancellationHandle _cancellationHandle = new();
    private readonly IScheduler _scheduler;

    private RemoteSettingsStatus _status = RemoteSettingsStatus.None;

    public RemoteSettingsService(
        ISettingsApiClient settingsApiClient,
        IErrorReporting errorReporting,
        Lazy<IEnumerable<IRemoteSettingsStateAware>> remoteSettingsStateAware,
        ILogger<RemoteSettingsService> logger)
    {
        _settingsApiClient = settingsApiClient;
        _errorReporting = errorReporting;
        _remoteSettingsStateAware = remoteSettingsStateAware;
        _logger = logger;

        _scheduler =
            new HandlingCancellationSchedulerDecorator(
                nameof(AccountService),
                logger,
                new LoggingExceptionsSchedulerDecorator(
                    nameof(AccountService),
                    logger,
                    new SerialScheduler()));
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

    private async Task SetUpAsync(CancellationToken cancellationToken)
    {
        if (_status is RemoteSettingsStatus.Succeeded)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("Remote settings set up started");
        SetState(RemoteSettingsStatus.SettingUp);

        try
        {
            var settings = await GetSettingsAsync(cancellationToken).ConfigureAwait(false);
            HandleSettingsChange(settings);
        }
        catch (Exception ex) when (ex.IsDriveClientException())
        {
            _logger.LogWarning("Remote settings set up failed: {Error}", ex.Message);
            SetState(RemoteSettingsStatus.Failed);

            return;
        }

        _logger.LogInformation("Remote settings set up succeeded");
        SetState(RemoteSettingsStatus.Succeeded);
    }

    private void CancelSetUp()
    {
        if (_status is RemoteSettingsStatus.None)
        {
            return;
        }

        var settings = new GeneralSettings { IsSendingCrashReportsEnabled = false };
        HandleSettingsChange(settings);

        _logger.LogInformation("Remote settings set up has been cancelled");

        SetState(RemoteSettingsStatus.None);
    }

    private async Task<GeneralSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var settingsResponse = await _settingsApiClient.GetAsync(cancellationToken).ThrowOnFailure().ConfigureAwait(false);

        return settingsResponse.Settings;
    }

    private void HandleSettingsChange(GeneralSettings settings)
    {
        _errorReporting.IsEnabled = settings.IsSendingCrashReportsEnabled;

        foreach (var listener in _remoteSettingsStateAware.Value)
        {
            listener.OnRemoteSettingsChanged(settings.IsTelemetryEnabled);
        }
    }

    private void SetState(RemoteSettingsStatus status)
    {
        _status = status;
    }

    private void Schedule(Action action)
    {
        Schedule(_ =>
            {
                action();
                return Task.CompletedTask;
            });
    }

    private void Schedule(Func<CancellationToken, Task> action)
    {
        var cancellationToken = _cancellationHandle.Token;

        _scheduler.Schedule(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            return action(cancellationToken);
        });
    }
}
