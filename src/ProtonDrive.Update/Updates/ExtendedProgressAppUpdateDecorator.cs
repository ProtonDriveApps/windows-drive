using System;
using System.Threading.Tasks;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.Update.Updates;

/// <summary>
/// Extends duration of the update state representing progress (checking and downloading) up to
/// specified minimum progress duration. Required for short state change to be noticed in the UI.
/// </summary>
internal class ExtendedProgressAppUpdateDecorator : INotifyingAppUpdate
{
    private readonly TimeSpan _minProgressDuration;
    private readonly INotifyingAppUpdate _origin;

    private readonly SerialScheduler _notifyScheduler;
    private readonly CancellationHandle _cancellationHandle = new();

    private DateTime _progressStartedAt;
    private IAppUpdateState? _prevState;

    public ExtendedProgressAppUpdateDecorator(TimeSpan minProgressDuration, INotifyingAppUpdate origin)
    {
        _minProgressDuration = minProgressDuration;
        _origin = origin;

        _origin.StateChanged += AppUpdate_StateChanged;
        _notifyScheduler = new SerialScheduler();
    }

    public event EventHandler<IAppUpdateState>? StateChanged;

    public void StartCheckingForUpdate(bool earlyAccess, bool manual) => _origin.StartCheckingForUpdate(earlyAccess, manual);

    public void StartUpdating(bool auto) => _origin.StartUpdating(auto);

    public Task<bool> TryInstallDownloadedUpdateAsync() => _origin.TryInstallDownloadedUpdateAsync();

    private void AppUpdate_StateChanged(object? sender, IAppUpdateState state)
    {
        if (ProgressStarted(state))
        {
            HandleProgressStart();
        }
        else if (ProgressEnded(state))
        {
            HandleProgressEnd();
        }

        _prevState = state;
        _notifyScheduler.Schedule(() => OnStateChanged(state));
    }

    private bool ProgressStarted(IAppUpdateState state)
    {
        return _prevState?.Status.InProgress() != true && state.Status.InProgress();
    }

    private bool ProgressEnded(IAppUpdateState state)
    {
        return _prevState?.Status.InProgress() == true && !state.Status.InProgress();
    }

    private void HandleProgressStart()
    {
        _progressStartedAt = DateTime.UtcNow;
        _cancellationHandle.Cancel();
    }

    private void HandleProgressEnd()
    {
        var requiredDelay = _progressStartedAt + _minProgressDuration - DateTime.UtcNow;

        if (requiredDelay > _minProgressDuration)
        {
            requiredDelay = _minProgressDuration;
        }

        if (requiredDelay > TimeSpan.Zero)
        {
            _notifyScheduler.Schedule(() => Delay(requiredDelay));
        }

        _progressStartedAt = DateTime.MinValue;
    }

    private async Task Delay(TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, _cancellationHandle.Token).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void OnStateChanged(IAppUpdateState state)
    {
        StateChanged?.Invoke(this, state);
    }
}
