using CommunityToolkit.Mvvm.Messaging;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.EarlyAccess;
using ProtonDrive.App.Services;
using ProtonDrive.App.Settings;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Offline;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Update;

namespace ProtonDrive.App.Update;

internal sealed class UpdateService
    : IDisposable, IUpdateService, IEarlyAccessStateAware, ISessionStateAware, IStoppableService, IRecipient<AppUpdateRequiredMessage>
{
    private readonly UpdateConfig _updateConfig;
    private readonly ClientInstanceSettings _clientInstanceSettings;
    private readonly INotifyingAppUpdate _appUpdate;
    private readonly IClock _clock;
    private readonly ISchedulerTimer _checkForUpdatesTimer;
    private readonly CoalescingAction _handleStateChange;
    private readonly TimeSpan _autoUpdateRetryInterval;

    private bool? _earlyAccessIsEnabled;
    private bool _stopping;
    private IAppUpdateState _appUpdateState = new EmptyAppUpdateState();
    private AppUpdateStatus _prevStatus;
    private DateTime _lastCheckedAt;
    private bool _requestedManualCheck;
    private bool _manualCheck;
    private bool _updateRequired;
    private bool _sessionIsStarted;

    public UpdateService(
        UpdateConfig updateConfig,
        ClientInstanceSettings clientInstanceSettings,
        INotifyingAppUpdate appUpdate,
        IScheduler scheduler,
        IMessenger messenger,
        IClock clock)
    {
        _updateConfig = updateConfig;
        _clientInstanceSettings = clientInstanceSettings;
        _appUpdate = appUpdate;
        _clock = clock;

        _autoUpdateRetryInterval = _updateConfig.AutoUpdateRetryInterval.RandomizedWithDeviation(0.2);

        _appUpdate.StateChanged += OnAppUpdateStateChanged;
        messenger.RegisterAll(this);

        _checkForUpdatesTimer = scheduler.CreateTimer();
        _checkForUpdatesTimer.Interval = updateConfig.CheckInterval.RandomizedWithDeviation(0.2);
        _checkForUpdatesTimer.Tick += OnCheckForUpdatesTimerTick;

        _handleStateChange = new CoalescingAction(HandleStateChange);
    }

    public event EventHandler<UpdateState>? StateChanged;

    public void StartCheckingForUpdate()
    {
        StartCheckingForUpdate(manualCheck: true);
    }

    public void StartUpdating()
    {
        _appUpdate.StartUpdating(auto: false);
    }

    public Task<bool> TryInstallDownloadedUpdateAsync()
    {
        return _appUpdate.TryInstallDownloadedUpdateAsync();
    }

    public void Dispose()
    {
        _checkForUpdatesTimer.Dispose();
    }

    void IEarlyAccessStateAware.OnEarlyAccessStateChanged(EarlyAccessStatus status)
    {
        // Upon app start, a cached value is notified.
        // Further notifications contain a change due to manaul user action.
        var isManualChange = _earlyAccessIsEnabled is not null;

        _earlyAccessIsEnabled = status is EarlyAccessStatus.Enabled;

        if (!_sessionIsStarted)
        {
            return;
        }

        StartCheckingForUpdate(isManualChange);
        _checkForUpdatesTimer.Start();
    }

    void ISessionStateAware.OnSessionStateChanged(SessionState value)
    {
        _sessionIsStarted = value.Status == SessionStatus.Started;

        // When a forced update is required we bypass both the session and the early-access gate:
        // the user can't use the app on this version, so the timer must start to download the update regardless of whether they're signed in.
        if (!_updateRequired)
        {
            if (!_sessionIsStarted || _earlyAccessIsEnabled is null)
            {
                _checkForUpdatesTimer.Stop();
                return;
            }
        }

        StartCheckingForUpdate(manualCheck: false);
        _checkForUpdatesTimer.Start();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stopping = true;
        _checkForUpdatesTimer.Stop();

        return Task.CompletedTask;
    }

    public void Receive(AppUpdateRequiredMessage message)
    {
        _updateRequired = true;

        _handleStateChange.Run();
    }

    private void StartCheckingForUpdate(bool manualCheck)
    {
        _requestedManualCheck |= manualCheck;

        if (!manualCheck && _clock.UtcNow - _lastCheckedAt <= _updateConfig.CheckInterval)
        {
            return;
        }

        _appUpdate.StartCheckingForUpdate(_earlyAccessIsEnabled ?? false, manualCheck);
        _lastCheckedAt = _clock.UtcNow;
    }

    private void OnAppUpdateStateChanged(object? sender, IAppUpdateState state)
    {
        _appUpdateState = state;
        _handleStateChange.Run();
    }

    private void OnCheckForUpdatesTimerTick(object? sender, EventArgs e)
    {
        StartCheckingForUpdate(false);
    }

    private bool RequiredUpdateCanBeExecuted()
    {
        if (_clientInstanceSettings.LastAutoUpdateTime is not { } lastAutoUpdateTime)
        {
            return true;
        }

        return _clock.UtcNow > lastAutoUpdateTime.Add(_autoUpdateRetryInterval);
    }

    private void HandleStateChange()
    {
        if (_stopping)
        {
            return;
        }

        var appUpdateState = _appUpdateState;

        var updateState = new UpdateState(appUpdateState)
        {
            ManualCheck = _manualCheck,
            UpdateRequired = _updateRequired,
        };

        OnUpdateStateChanged(updateState);

        if (_updateRequired
            && !_manualCheck
            && appUpdateState is { IsReady: true, Status: AppUpdateStatus.Ready }
            && RequiredUpdateCanBeExecuted())
        {
            _clientInstanceSettings.SetLastAutoUpdateTime(_clock.UtcNow);
            _appUpdate.StartUpdating(auto: true);
        }

        HandleManualCheck(appUpdateState.Status);
    }

    private void OnUpdateStateChanged(UpdateState state)
    {
        StateChanged?.Invoke(this, state);
    }

    private void HandleManualCheck(AppUpdateStatus status)
    {
        if (status != _prevStatus && status == AppUpdateStatus.Checking)
        {
            _manualCheck = _requestedManualCheck;
            _requestedManualCheck = false;
        }

        _prevStatus = status;
    }
}
