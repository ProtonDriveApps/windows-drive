using ProtonDrive.Shared.Features;
using ProtonDrive.Sync.Shared.SyncActivity;

namespace ProtonDrive.App.Instrumentation.Observability;

internal sealed class LegacyPhotoUploadSuccessMeter : LegacySuccessMeterBase, IFeatureFlagsAware
{
    private bool _sdkIsEnabled;

    public LegacyPhotoUploadSuccessMeter(AttemptRetryMonitors attemptRetryMonitors)
        : base(attemptRetryMonitors.UploadAttemptRetryMonitor)
    {
    }

    void IFeatureFlagsAware.OnFeatureFlagsChanged(IReadOnlyDictionary<Feature, bool> features)
    {
        _sdkIsEnabled = features[Feature.DriveWindowsSdkUploadPhoto];
    }

    public override bool CanProcessItem(SyncActivityItem<long> item)
    {
        if (_sdkIsEnabled)
        {
            return false;
        }

        return item.ActivityType is SyncActivityType.Upload;
    }
}
