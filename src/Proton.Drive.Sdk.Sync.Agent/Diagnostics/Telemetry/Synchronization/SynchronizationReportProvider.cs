using Proton.Drive.Sdk.Sync.Agent.Account;
using Proton.Drive.Shared.Telemetry;

namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Telemetry.Synchronization;

internal sealed class SynchronizationReportProvider(
    SyncStatistics syncStatistics,
    SharedWithMeItemCounters sharedWithMeItemCounters,
    OpenedDocumentsCounters openedDocumentsCounters) : IPeriodicTelemetryReportProvider, IUserStateAware
{
    private bool? _userHasAPaidPlan;

    public IEnumerable<TelemetryEvent> GetPeriodicReport()
    {
        try
        {
            return SynchronizationReportFactory.CreateReport(
                syncStatistics,
                _userHasAPaidPlan,
                sharedWithMeItemCounters,
                openedDocumentsCounters);
        }
        finally
        {
            Reset();
        }
    }

    public void Reset()
    {
        syncStatistics.Reset();
        sharedWithMeItemCounters.Reset();
        openedDocumentsCounters.Reset();
    }

    void IUserStateAware.OnUserStateChanged(UserState value)
    {
        if (value.SubscriptionPlanCode is not null)
        {
            _userHasAPaidPlan = value.SubscriptionPlanCode != PeriodicReportConstants.FreePlan;
        }
    }
}
