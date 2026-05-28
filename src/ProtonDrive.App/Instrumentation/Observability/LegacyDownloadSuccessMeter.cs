using ProtonDrive.Shared.Features;
using ProtonDrive.Sync.Shared.SyncActivity;

namespace ProtonDrive.App.Instrumentation.Observability;

internal sealed class LegacyDownloadSuccessMeter : LegacySuccessMeterBase, IFeatureFlagsAware
{
    private bool _sdkIsEnabled;

    public LegacyDownloadSuccessMeter(AttemptRetryMonitors attemptRetryMonitors)
        : base(attemptRetryMonitors.DownloadAttemptRetryMonitor)
    {
    }

    void IFeatureFlagsAware.OnFeatureFlagsChanged(IReadOnlyDictionary<Feature, bool> features)
    {
        _sdkIsEnabled = features[Feature.DriveWindowsSdkDownloadMain];
    }

    public override bool CanProcessItem(SyncActivityItem<long> item)
    {
        if (_sdkIsEnabled)
        {
            return false;
        }

        return item.ActivityType is SyncActivityType.Download && item.Source is SyncActivitySource.OnDemandFileHydration;
    }
}
