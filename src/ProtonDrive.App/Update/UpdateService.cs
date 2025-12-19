using CommunityToolkit.Mvvm.Messaging;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.EarlyAccess;
using ProtonDrive.App.Services;
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
    private readonly INotifyingAppUpdate _appUpdate;
    private readonly ISchedulerTimer _timer;
    private readonly CoalescingAction _handleStateChange;

    private bool? _earlyAccessIsEnabled;
    private bool _stopping;
    private IAppUpdateState _state = new EmptyAppUpdateState();
    private AppUpdateStatus _prevStatus;
    private DateTime _lastCheckedAt;
    private bool _requestedManualCheck;
    private bool _manualCheck;
    private bool _updateRequired;
    private bool _sessionIsStarted;

    public UpdateService(
        UpdateConfig updateConfig,
        INotifyingAppUpdate appUpdate,
        IScheduler scheduler,
        IMessenger messenger)
    {
        _updateConfig = updateConfig;
        _appUpdate = appUpdate;

        _appUpdate.StateChanged += OnAppUpdateStateChanged;
        messenger.RegisterAll(this);

        _timer = scheduler.CreateTimer();
        _timer.Interval = updateConfig.CheckInterval.RandomizedWithDeviation(0.2);
        _timer.Tick += OnTimerTick;

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
        _timer.Dispose();
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
        _timer.Start();
    }

    void ISessionStateAware.OnSessionStateChanged(SessionState value)
    {
        _sessionIsStarted = value.Status == SessionStatus.Started;

        if (!_sessionIsStarted || _earlyAccessIsEnabled is null)
        {
            _timer.Stop();
            return;
        }

        StartCheckingForUpdate(manualCheck: false);
        _timer.Start();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stopping = true;
        _timer.Stop();

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

        if (!manualCheck && DateTime.UtcNow - _lastCheckedAt <= _updateConfig.CheckInterval)
        {
            return;
        }

        _appUpdate.StartCheckingForUpdate(_earlyAccessIsEnabled ?? false, manualCheck);
        _lastCheckedAt = DateTime.UtcNow;
    }

    private void OnAppUpdateStateChanged(object? sender, IAppUpdateState state)
    {
        _state = state;
        _handleStateChange.Run();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        StartCheckingForUpdate(false);
    }

    private void HandleStateChange()
    {
        if (_stopping)
        {
            return;
        }

        var state = _state;

        var updateState = new UpdateState(state)
        {
            ManualCheck = _manualCheck,
            UpdateRequired = _updateRequired,
        };

        OnUpdateStateChanged(updateState);

        HandleManualCheck(state.Status);
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
