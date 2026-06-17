using Microsoft.Extensions.Logging;
using ProtonDrive.App.Volumes;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.App.Sync;

internal sealed class SyncLifecycleService : ISyncLifecycleService, IMainVolumeStateAware
{
    private readonly Lazy<IEnumerable<ISyncLifecycleStateAware>> _syncLifecycleObservers;
    private readonly ILogger<SyncLifecycleService> _logger;
    private readonly CoalescingAction _stateChangeHandler;

    private SyncLifecycleStatus _state;
    private bool _enabled;
    private VolumeState _mainVolumeState = VolumeState.Idle;

    public SyncLifecycleService(
        Lazy<IEnumerable<ISyncLifecycleStateAware>> syncLifecycleObservers,
        ILogger<SyncLifecycleService> logger)
    {
        _syncLifecycleObservers = syncLifecycleObservers;
        _logger = logger;

        _stateChangeHandler = _logger.GetCoalescingActionWithExceptionsLogging(HandleStateChange, nameof(SyncLifecycleService));
    }

    public void Disable()
    {
        _enabled = false;
        ScheduleStateChangeHandling();
    }

    public void Enable()
    {
        _enabled = true;
        ScheduleStateChangeHandling();
    }

    void IMainVolumeStateAware.OnMainVolumeStateChanged(VolumeState value)
    {
        _mainVolumeState = value;
        ScheduleStateChangeHandling();
    }

    internal Task WaitForCompletionAsync()
    {
        return _stateChangeHandler.WaitForCompletionAsync();
    }

    private void HandleStateChange()
    {
        var state = _enabled && _mainVolumeState.Status is VolumeStatus.Ready
            ? SyncLifecycleStatus.Enabled
            : SyncLifecycleStatus.Disabled;

        if (!ValueExtensions.TryUpdate(ref _state, state))
        {
            return;
        }

        _logger.LogInformation("Sync lifecycle state changed to {SyncLifecycleStatus}", state);

        OnStateChanged(state);
    }

    private void OnStateChanged(SyncLifecycleStatus state)
    {
        foreach (var observer in _syncLifecycleObservers.Value)
        {
            observer.OnSyncLifecycleStateChanged(state);
        }
    }

    private void ScheduleStateChangeHandling()
    {
        _stateChangeHandler.Run();
    }
}
