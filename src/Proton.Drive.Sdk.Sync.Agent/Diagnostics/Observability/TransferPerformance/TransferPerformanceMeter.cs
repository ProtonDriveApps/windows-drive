using Proton.Drive.Sdk.Sync.Agent.Account;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.SyncActivity;

namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Observability.TransferPerformance;

internal sealed class TransferPerformanceMeter : ISyncActivityAware, IAccountSwitchingAware
{
    private readonly TransferPerformanceMonitors _monitors;

    public TransferPerformanceMeter(TransferPerformanceMonitors monitors)
    {
        _monitors = monitors;
    }

    void ISyncActivityAware.OnSyncActivityChanged(SyncActivityItem<long> item)
    {
        if (item.Source is not SyncActivitySource.OperationExecution and not SyncActivitySource.OnDemandFileHydration)
        {
            return;
        }

        if (item.ActivityType is not SyncActivityType.Upload and not SyncActivityType.Download)
        {
            return;
        }

        var key = (item.ActivityType, TransferContext.Background, TransferPipeline.Default);
        var monitor = _monitors[key];

        monitor.HandleProgress(item);
    }

    void IAccountSwitchingAware.OnAccountSwitched()
    {
        // We clear data upon account switching, because same ID values would be reused for different files
        _monitors.Clear();
    }
}
