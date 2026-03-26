using Proton.Drive.Sdk.Telemetry;
using Proton.Sdk.Telemetry;
using ProtonDrive.Shared.Metrics;

namespace ProtonDrive.Client.Sdk.Metrics;

internal sealed class SdkMetrics(UploadMetrics uploadMetrics, DownloadMetrics downloadMetrics, IntegrityMetrics integrityMetrics) : IMetricsRecorder
{
    public const string VolumeTypeKeyName = "volumeType";
    public const string AttemptStatusKeyName = "status";
    public const string FailureTypeKeyName = "type";
    public const string UserPlanKeyName = "userPlan";

    public void Record(IMetricEvent metricEvent)
    {
        switch (metricEvent)
        {
            case UploadEvent uploadEvent:
                uploadMetrics.Record(uploadEvent);
                break;

            case DownloadEvent downloadEvent:
                downloadMetrics.Record(downloadEvent);
                break;

            case DecryptionErrorEvent decryptionErrorEvent:
                integrityMetrics.Record(decryptionErrorEvent);
                break;

            case VerificationErrorEvent verificationErrorEvent:
                integrityMetrics.Record(verificationErrorEvent);
                break;

            case BlockVerificationErrorEvent uploadBlockVerificationErrorEvent:
                integrityMetrics.Record(uploadBlockVerificationErrorEvent);
                break;
        }
    }

    public void Record(MetricEvent metricEvent)
    {
        switch (metricEvent)
        {
            case UploadChecksumVerificationAttemptEvent uploadChecksumVerificationAttemptEvent:
                integrityMetrics.Record(uploadChecksumVerificationAttemptEvent);
                break;

            case DownloadChecksumVerificationAttemptEvent downloadChecksumVerificationAttemptEvent:
                integrityMetrics.Record(downloadChecksumVerificationAttemptEvent);
                break;
        }
    }
}
