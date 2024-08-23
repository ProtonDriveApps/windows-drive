using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.Update.Updates;

/// <summary>
/// Performs series of asynchronous update checking, downloading and verifying operations
/// and notifies about the state change.
/// </summary>
internal class NotifyingAppUpdate : INotifyingAppUpdate
{
    private readonly CoalescingAction _checkForUpdate;

    private IAppUpdate _update;
    private readonly ILogger<NotifyingAppUpdate> _logger;
    private AppUpdateStatus _status;
    private bool _earlyAccess;
    private bool _manual;
    private volatile bool _requestedEarlyAccess;
    private volatile bool _requestedManual;

    public NotifyingAppUpdate(IAppUpdate update, ILogger<NotifyingAppUpdate> logger)
    {
        _update = update;
        _logger = logger;

        _checkForUpdate = new CoalescingAction(SafeCheckForUpdate);
    }

    public event EventHandler<IAppUpdateState>? StateChanged;

    public void StartCheckingForUpdate(bool earlyAccess, bool manual)
    {
        if (_checkForUpdate.Running)
        {
            if (_requestedEarlyAccess == earlyAccess && _requestedManual == manual)
            {
                return;
            }

            _checkForUpdate.Cancel();
        }

        _requestedEarlyAccess = earlyAccess;
        _requestedManual = manual;
        _checkForUpdate.Run();
    }

    public void StartUpdating(bool auto)
    {
        _update.StartUpdating(auto);

        // The state change to Updating triggers the app to exit.
        // State is changed to Updating only if update has been successfully started (not raised an exception).
        OnStateChanged(AppUpdateStatus.Updating);
    }

    public async Task<bool> TryInstallDownloadedUpdateAsync()
    {
        try
        {
            _update = _update.GetCachedLatest(earlyAccess: true);
            if (!_update.IsAvailable)
            {
                return false;
            }

            if (string.IsNullOrEmpty(_update.FilePath) || !File.Exists(_update.FilePath))
            {
                _update = _update.GetCachedLatest(earlyAccess: false);
                if (!_update.IsAvailable || string.IsNullOrEmpty(_update.FilePath) || !File.Exists(_update.FilePath))
                {
                    return false;
                }
            }

            _update = await _update.ValidateAsync().ConfigureAwait(false);
            if (!_update.IsReady)
            {
                return false;
            }

            _update.StartUpdating(true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to try installing a downloaded update");
            return false;
        }
    }

    private async Task SafeCheckForUpdate(CancellationToken cancellationToken)
    {
        try
        {
            await UnsafeCheckForUpdate(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            HandleCancellation();
        }
        catch (AppUpdateException)
        {
            HandleFailure();
        }
    }

    private async Task UnsafeCheckForUpdate(CancellationToken cancellationToken)
    {
        _earlyAccess = _requestedEarlyAccess;
        _manual = _requestedManual;
        _status = AppUpdateStatus.Checking;

        HandleSuccess(await _update.GetLatestAsync(_earlyAccess, _manual).ConfigureAwait(false), cancellationToken);

        if (_update.IsAvailable)
        {
            HandleSuccess(await _update.ValidateAsync().ConfigureAwait(false), cancellationToken);

            if (!_update.IsReady)
            {
                _status = AppUpdateStatus.Downloading;
                OnStateChanged();

                HandleSuccess(await _update.DownloadAsync().ConfigureAwait(false), cancellationToken);
                HandleSuccess(await _update.ValidateAsync().ConfigureAwait(false), cancellationToken);

                if (!_update.IsReady)
                {
                    _status = AppUpdateStatus.DownloadFailed;
                    OnStateChanged();

                    return;
                }
            }
        }

        _status = _update.IsReady ? AppUpdateStatus.Ready : AppUpdateStatus.None;
        OnStateChanged();
    }

    private void HandleSuccess(IAppUpdate update, CancellationToken cancellationToken)
    {
        _update = update;
        cancellationToken.ThrowIfCancellationRequested();

        OnStateChanged();
    }

    private void HandleCancellation()
    {
        _status = AppUpdateStatus.None;
        OnStateChanged();
    }

    private void HandleFailure()
    {
        switch (_status)
        {
            case AppUpdateStatus.Checking:
                _status = AppUpdateStatus.CheckFailed;
                break;
            case AppUpdateStatus.Downloading:
                _status = AppUpdateStatus.DownloadFailed;
                break;
            default:
                _status = AppUpdateStatus.None;
                break;
        }

        OnStateChanged();
    }

    private void OnStateChanged()
    {
        OnStateChanged(_status);
    }

    private void OnStateChanged(AppUpdateStatus status)
    {
        var eventArgs = new AppUpdateState(_update, status);
        StateChanged?.Invoke(this, eventArgs);
    }
}
