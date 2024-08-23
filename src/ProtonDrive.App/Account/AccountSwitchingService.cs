using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Threading;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Settings;
using ProtonDrive.App.Sync;
using ProtonDrive.Shared.Repository;
using ProtonDrive.Sync.Shared.SyncActivity;

namespace ProtonDrive.App.Account;

internal sealed class AccountSwitchingService : IAccountSwitchingService, ISyncStateAware
{
    private readonly IRepository<UserSettings> _settingsRepository;
    private readonly Lazy<IEnumerable<IAccountSwitchingHandler>> _accountSwitchingHandlers;
    private readonly Lazy<IEnumerable<IAccountSwitchingAware>> _accountSwitchingAwareObjects;
    private readonly ILogger<AccountSwitchingService> _logger;

    private readonly AsyncManualResetEvent _synchronizationTerminated = new(initialState: true);

    public AccountSwitchingService(
        IRepository<UserSettings> settingsRepository,
        Lazy<IEnumerable<IAccountSwitchingHandler>> accountSwitchingHandlers,
        Lazy<IEnumerable<IAccountSwitchingAware>> accountSwitchingAwareObjects,
        ILogger<AccountSwitchingService> logger)
    {
        _settingsRepository = settingsRepository;
        _accountSwitchingHandlers = accountSwitchingHandlers;
        _accountSwitchingAwareObjects = accountSwitchingAwareObjects;
        _logger = logger;
    }

    public bool IsAccountSwitchingRequired(string? userId)
    {
        var previousUserId = GetPreviousUser();

        if (previousUserId is null)
        {
            // Previous user account is not known, assume it has not changed
            SetPreviousUser(userId);

            return false;
        }

        if (previousUserId == userId)
        {
            // User account has not changed
            return false;
        }

        _logger.LogWarning("Account switching detected. Previous user ID={PreviousUserId}, new user ID={NewUserId}", previousUserId, userId);

        return true;
    }

    public async Task<bool> SwitchAccountAsync(string? userId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Switching user account started");

        ClearPreviousUser();

        _logger.LogInformation("Waiting for synchronization to terminate");

        await _synchronizationTerminated.WaitAsync(cancellationToken).ConfigureAwait(false);

        var succeeded = await HandleAccountSwitchingAsync(cancellationToken).ConfigureAwait(false);

        if (!succeeded)
        {
            _logger.LogInformation("Switching user account failed");

            return false;
        }

        SetPreviousUser(userId);

        _logger.LogInformation("Switching user account succeeded");

        OnAccountSwitched();

        return true;
    }

    void ISyncStateAware.OnSyncStateChanged(SyncState value)
    {
        if (value.Status is SyncStatus.Terminated)
        {
            _synchronizationTerminated.Set();
        }
        else
        {
            _synchronizationTerminated.Reset();
        }
    }

    private async Task<bool> HandleAccountSwitchingAsync(CancellationToken cancellationToken)
    {
        var succeeded = true;

        foreach (var handler in _accountSwitchingHandlers.Value)
        {
            succeeded &= await handler.HandleAccountSwitchingAsync(cancellationToken).ConfigureAwait(false);
        }

        return succeeded;
    }

    private void OnAccountSwitched()
    {
        foreach (var listener in _accountSwitchingAwareObjects.Value)
        {
            listener.OnAccountSwitched();
        }
    }

    private string? GetPreviousUser()
    {
        return _settingsRepository.Get()?.UserId;
    }

    private void ClearPreviousUser()
    {
        // Emptying the user ID string to indicate that switching user account has been started
        SetPreviousUser(string.Empty);
    }

    private void SetPreviousUser(string? userId)
    {
        var settings = _settingsRepository.Get() ?? new UserSettings();

        settings.UserId = userId;

        _settingsRepository.Set(settings);
    }
}
