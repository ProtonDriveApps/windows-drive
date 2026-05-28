using ProtonDrive.Shared.Features;
using ProtonDrive.Sync.Shared.SyncActivity;

namespace ProtonDrive.App.Instrumentation.Observability;

internal sealed class LegacyUploadSuccessMeter : LegacySuccessMeterBase, IFeatureFlagsAware
{
    private bool _sdkIsEnabled;

    public LegacyUploadSuccessMeter(AttemptRetryMonitors attemptRetryMonitors)
        : base(attemptRetryMonitors.UploadAttemptRetryMonitor)
    {
    }

    void IFeatureFlagsAware.OnFeatureFlagsChanged(IReadOnlyDictionary<Feature, bool> features)
    {
        _sdkIsEnabled = features[Feature.DriveWindowsSdkUploadMain];
    }

    public override bool CanProcessItem(SyncActivityItem<long> item)
    {
        if (_sdkIsEnabled)
        {
            return false;
        }

        // Since the upload process hasn't been initiated,
        // we don't classify the inability to open or read the file (or other issues during this stage) as an "upload failure".
        if (item.Stage == SyncActivityStage.Preparation)
        {
            return false;
        }

        return item.ActivityType is SyncActivityType.Upload;
    }
}
