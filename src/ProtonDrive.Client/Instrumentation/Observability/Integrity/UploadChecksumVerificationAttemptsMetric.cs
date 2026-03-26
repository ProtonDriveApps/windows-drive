using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.Client.Instrumentation.Observability.Integrity;

public sealed record UploadChecksumVerificationAttemptsMetric : ObservabilityMetric
{
    public UploadChecksumVerificationAttemptsMetric(long value, IReadOnlyDictionary<string, string> labels)
        : base(
            "drive_upload_verifier_attempts_total",
            Version: 1,
            DateTime.UtcNow.ToUnixTimeSeconds(),
            new ObservabilityMetricProperties(value, labels))
    {
    }
}
