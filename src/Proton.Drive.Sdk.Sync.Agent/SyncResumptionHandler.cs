using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Services;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.SyncActivity;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Agent;

internal sealed class SyncResumptionHandler : ISyncStateAware, IStoppableService, IDisposable
{
    private static readonly TimeSpan ResumePausedSyncCheckInterval = TimeSpan.FromMinutes(1);

    private readonly ISyncService _syncService;
    private readonly IClock _clock;
    private readonly ILogger<SyncResumptionHandler> _logger;
    private readonly ISchedulerTimer _timer;
    private SyncState _syncState = SyncState.Idle;

    public SyncResumptionHandler(
        ISyncService syncService,
        IScheduler scheduler,
        IClock clock,
        ILogger<SyncResumptionHandler> logger)
    {
        _syncService = syncService;
        _clock = clock;
        _logger = logger;

        _timer = scheduler.CreateTimer();
        _timer.Interval = ResumePausedSyncCheckInterval;
        _timer.Tick += OnTick;
    }

    void ISyncStateAware.OnSyncStateChanged(SyncState value)
    {
        _syncState = value;

        if (value.Status is not SyncStatus.Paused)
        {
            _timer.Stop();
            return;
        }

        // Resume immediately if the deadline has already passed, otherwise poll until it does.
        if (ResumeIfTimedPauseExpired())
        {
            return;
        }

        _timer.Start();
    }

    Task IStoppableService.StopAsync(CancellationToken cancellationToken)
    {
        _timer.Stop();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        ResumeIfTimedPauseExpired();
    }

    private bool ResumeIfTimedPauseExpired()
    {
        if (_syncState.Status is not SyncStatus.Paused || _syncState.PausedUntil is not { } pauseUntil)
        {
            _timer.Stop();
            return true;
        }

        if (pauseUntil.UtcDateTime > _clock.UtcNow)
        {
            return false;
        }

        _logger.LogInformation("Timed pause expired, resuming sync automatically");
        _timer.Stop();
        _syncService.Resume();
        return true;
    }
}
