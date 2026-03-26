using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.Client.Instrumentation.Observability.Integrity;

public sealed record UploadBlockVerificationFailuresMetric : ObservabilityMetric
{
    public UploadBlockVerificationFailuresMetric(long value, IReadOnlyDictionary<string, string> labels)
        : base(
            "drive_sdk_integrity_block_verification_errors_total",
            Version: 1,
            DateTime.UtcNow.ToUnixTimeSeconds(),
            new ObservabilityMetricProperties(value, labels))
    {
    }
}
