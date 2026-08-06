using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Account;
using Proton.Drive.Sdk.Sync.Client;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Logging;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.App.Account;

internal sealed class ActivityService : IAccountStateAware, IUserStateAware, IDisposable
{
    internal const double QueryIntervalMaxDeviation = 0.2;

    private readonly IDriveUserApiClient _driveUserApiClient;
    private readonly ILogger<ActivityService> _logger;

    private readonly SingleAction _getIsActive;
    private readonly ISchedulerTimer _timer;
    private long _driveUsedSpace;

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

    void IUserStateAware.OnUserStateChanged(UserState value)
    {
        _driveUsedSpace = value.UsedDriveSpace;
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    internal Task WaitForCompletionAsync()
    {
        return _getIsActive.WaitForCompletionAsync();
    }

    private async Task GetIsActiveAsync(CancellationToken cancellationToken)
    {
        if (_driveUsedSpace == 0)
        {
            return;
        }

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
