using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.Services;
using ProtonDrive.Shared.Offline;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.App.Account;

internal sealed class AccountService : IAccountService, IStoppableService, ISessionStateAware
{
    private readonly IAccountSwitchingService _accountSwitchingService;
    private readonly IUserService _userService;
    private readonly IOfflineService _offlineService;
    private readonly Lazy<IEnumerable<IAccountStateAware>> _accountStateAware;
    private readonly ILogger<AccountService> _logger;

    private readonly CancellationHandle _cancellationHandle = new();
    private readonly IScheduler _scheduler;

    private volatile bool _stopping;
    private SessionState _sessionState = SessionState.None;
    private AccountState _state = AccountState.None;

    public AccountService(
        IAccountSwitchingService accountSwitchingService,
        IUserService userService,
        IOfflineService offlineService,
        Lazy<IEnumerable<IAccountStateAware>> accountStateAware,
        ILogger<AccountService> logger)
    {
        _accountSwitchingService = accountSwitchingService;
        _userService = userService;
        _offlineService = offlineService;
        _accountStateAware = accountStateAware;
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

    public AccountState State
    {
        get => _state;
        private set
        {
            _state = value;
            OnStateChanged(value);
        }
    }

    public Task SetUpAccountAsync()
    {
        ForceOnline();
        return Schedule(SetUpAccountAsync);
    }

    void ISessionStateAware.OnSessionStateChanged(SessionState value)
    {
        var prevStatus = _sessionState.Status;

        _sessionState = value;

        if (value.Status != prevStatus)
        {
            HandleExternalStateChange();
        }
    }

    async Task IStoppableService.StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{nameof(AccountService)} is stopping");
        _stopping = true;
        _cancellationHandle.Cancel();

        await _userService.StopAsync().ConfigureAwait(false);
        await WaitForCompletionAsync().ConfigureAwait(false);

        _logger.LogInformation($"{nameof(AccountService)} stopped");
    }

    internal Task WaitForCompletionAsync()
    {
        // Wait for all scheduled tasks to complete
        return _scheduler.Schedule(() => false);
    }

    private void HandleExternalStateChange()
    {
        if (_sessionState.Status == SessionStatus.Started)
        {
            _logger.LogDebug("Scheduling user account set up");
            Schedule(SetUpAccountAsync);
        }
        else
        {
            if (State.Status != AccountStatus.None)
            {
                _logger.LogDebug("Scheduling cancellation of user account set up");
            }

            _cancellationHandle.Cancel();
            Schedule(CancelSetUpAsync);
        }
    }

    private async Task SetUpAccountAsync(CancellationToken cancellationToken)
    {
        if (_stopping)
        {
            return;
        }

        SetState(AccountStatus.None);

        var session = _sessionState;

        if (!IsSessionStarted(session))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("Setting up user account");
        SetState(AccountStatus.SettingUp);
        _userService.Start(session.Scopes);

        var errorCode =
            await ProcessAccountSwitchingAsync(session, cancellationToken).ConfigureAwait(false) ??
            await SetUpAccountAsync(session, cancellationToken).ConfigureAwait(false) ??
            AccountErrorCode.None;

        cancellationToken.ThrowIfCancellationRequested();

        if (errorCode != AccountErrorCode.None)
        {
            SetError(errorCode);

            return;
        }

        _logger.LogInformation("Setting up user account has succeeded");

        SetState(AccountStatus.Succeeded);
    }

    private bool IsSessionStarted(SessionState session)
    {
        if (session.Status != SessionStatus.Started)
        {
            _logger.LogWarning("Session is not started");

            return false;
        }

        return true;
    }

    private async Task CancelSetUpAsync(CancellationToken cancellationToken)
    {
        if (_stopping)
        {
            return;
        }

        if (State.Status == AccountStatus.None)
        {
            return;
        }

        _logger.LogInformation("Setting up user account has been cancelled");

        await _userService.StopAsync().ConfigureAwait(false);

        SetState(AccountStatus.None);
    }

    private async Task<AccountErrorCode?> ProcessAccountSwitchingAsync(SessionState session, CancellationToken cancellationToken)
    {
        if (!_accountSwitchingService.IsAccountSwitchingRequired(session.UserId))
        {
            return default;
        }

        var succeeded = await _accountSwitchingService.SwitchAccountAsync(session.UserId, cancellationToken).ConfigureAwait(false);

        if (!succeeded)
        {
            return AccountErrorCode.AccountSwitchingFailed;
        }

        return default;
    }

    private async Task<AccountErrorCode?> SetUpAccountAsync(SessionState session, CancellationToken cancellationToken)
    {
        var user = await _userService.GetUserAsync(cancellationToken).ConfigureAwait(false);
        if (user.IsEmpty)
        {
            _logger.LogWarning("Failed to get user account");
            return AccountErrorCode.DriveAccessFailed;
        }

        // Scopes are not refreshed without signing out and signing in again
        if (!session.Scopes.Contains("drive"))
        {
            _logger.LogWarning("User account is not eligible to access Proton Drive");
            return AccountErrorCode.NoDriveAccess;
        }

        if (user.IsDelinquent)
        {
            _logger.LogWarning("User account has unpaid invoices");
            return AccountErrorCode.Delinquent;
        }

        return default;
    }

    private void SetState(AccountStatus status)
    {
        State = new AccountState(status, AccountErrorCode.None);
    }

    private void SetError(AccountErrorCode errorCode)
    {
        State = new AccountState(AccountStatus.Failed, errorCode);
    }

    private void OnStateChanged(AccountState value)
    {
        foreach (var listener in _accountStateAware.Value)
        {
            listener.OnAccountStateChanged(value);
        }
    }

    private void ForceOnline()
    {
        if (_stopping)
        {
            return;
        }

        _offlineService.ForceOnline();
    }

    private Task Schedule(Func<CancellationToken, Task> action)
    {
        if (_stopping)
        {
            return Task.CompletedTask;
        }

        var cancellationToken = _cancellationHandle.Token;

        return _scheduler.Schedule(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                return action(cancellationToken);
            },
            cancellationToken);
    }
}
