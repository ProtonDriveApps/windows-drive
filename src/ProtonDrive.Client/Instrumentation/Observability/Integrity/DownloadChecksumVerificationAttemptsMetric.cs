using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.Client.Instrumentation.Observability.Integrity;

public sealed record DownloadChecksumVerificationAttemptsMetric : ObservabilityMetric
{
    public DownloadChecksumVerificationAttemptsMetric(long value, IReadOnlyDictionary<string, string> labels)
        : base(
            "drive_download_verifier_attempts_total",
            Version: 1,
            DateTime.UtcNow.ToUnixTimeSeconds(),
            new ObservabilityMetricProperties(value, labels))
    {
    }
}
