using Proton.Drive.Sdk.Sync.Shared.SyncActivity;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Configuration;
using TransferId = (Proton.Drive.Sdk.Sync.Shared.SyncActivity.SyncActivitySource ActivitySource, long FileId);

namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Observability.TransferPerformance;

internal sealed class TransferPerformanceMonitor
{
    private readonly TransferProgressMonitor<TransferId> _progressMonitor = new();
    private readonly TransferActivityMonitor<TransferId> _activityMonitor = new();
    private readonly TransferDurationMonitor _durationMonitor;

    private long _previousTotalProgress;

    public TransferPerformanceMonitor(AppConfig appConfig, IClock clock)
    {
        _durationMonitor = new TransferDurationMonitor(appConfig.MaxInactivityPeriodBetweenFileTransfers, clock);
    }

    public void HandleProgress(SyncActivityItem<long> item)
    {
        var key = (item.Source, item.Id);
        var position = (long)((item.Size ?? 0) * item.Progress.Ratio);

        // During preparation stage, we do not know whether on-demand file will be downloaded or not.
        // On demand file hydration always downloads a file.
        var isActive = item.Status is SyncActivityItemStatus.InProgress &&
            (item.Stage is SyncActivityStage.Execution || item.ActivityType is SyncActivityType.Upload || item.Source is SyncActivitySource.OnDemandFileHydration);

        var hasSucceeded = item.Status is SyncActivityItemStatus.Succeeded;

        _progressMonitor.UpdateProgress(key, position);

        if (hasSucceeded)
        {
            _progressMonitor.Remove(key);
        }

        var numberOfTransfersDelta = isActive switch
        {
            true => _activityMonitor.Add(key),
            false => _activityMonitor.Remove(key),
        };

        _durationMonitor.UpdateNumberOfTransfers(numberOfTransfersDelta);
    }

    /// <summary>
    /// Obtains transfer speed during the time interval between the previous call to <see cref="GetTransferSpeed"/> and now.
    /// </summary>
    /// <returns>Transfer speed in bytes/second, if any transfer was active during the interval; <see langword="null"/> otherwise.</returns>
    public double? GetTransferSpeed()
    {
        var duration = _durationMonitor.GetDurationIncrease();
        var bytesTransferred = GetProgressIncrease();

        if (duration <= TimeSpan.Zero || bytesTransferred < 0)
        {
            return null;
        }

        return bytesTransferred / duration.TotalSeconds;
    }

    public void Clear()
    {
        _progressMonitor.Clear();
        _activityMonitor.Clear();
        _durationMonitor.Clear();

        _previousTotalProgress = 0;
    }

    private long GetProgressIncrease()
    {
        var totalProgress = _progressMonitor.TotalProgress;
        var progress = totalProgress - _previousTotalProgress;
        _previousTotalProgress = totalProgress;

        return progress;
    }
}
