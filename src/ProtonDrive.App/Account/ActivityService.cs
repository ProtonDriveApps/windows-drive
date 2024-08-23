using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Client;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.App.Account;

internal sealed class ActivityService : IAccountStateAware, IDisposable
{
    internal const double QueryIntervalMaxDeviation = 0.2;

    private readonly IDriveUserApiClient _driveUserApiClient;
    private readonly ILogger<ActivityService> _logger;

    private readonly SingleAction _getIsActive;
    private readonly ISchedulerTimer _timer;

    public ActivityService(AppConfig appConfig, IDriveUserApiClient driveUserApiClient, IScheduler scheduler, ILogger<ActivityService> logger)
    {
        _driveUserApiClient = driveUserApiClient;
        _logger = logger;

        _getIsActive = _logger.GetSingleActionWithExceptionsLoggingAndCancellationHandling(GetIsActiveAsync, nameof(ActivityService));

        _timer = scheduler.CreateTimer();
        _timer.Interval = appConfig.ActivityQueryInterval.RandomizedWithDeviation(QueryIntervalMaxDeviation);
        _timer.Tick += (_, _) => _getIsActive.RunAsync();
    }

    void IAccountStateAware.OnAccountStateChanged(AccountState value)
    {
        switch (value.Status)
        {
            case AccountStatus.Succeeded:
                if (!_timer.IsEnabled)
                {
                    _timer.Start();
                    _getIsActive.RunAsync();
                }

                break;

            case AccountStatus.SettingUp:
                break;

            default:
                _timer.Stop();
                _getIsActive.Cancel();
                break;
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    internal Task WaitForCompletionAsync()
    {
        return _getIsActive.CurrentTask;
    }

    private async Task GetIsActiveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _driveUserApiClient.GetIsActiveAsync(cancellationToken).ThrowOnFailure().ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning("Failed to get whether user is active: {ErrorCode} {ErrorMessage}", ex.ResponseCode, ex.Message);
        }
    }
}
