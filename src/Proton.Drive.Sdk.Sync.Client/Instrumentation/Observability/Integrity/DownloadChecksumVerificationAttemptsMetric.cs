using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Integrity;

public sealed record DownloadChecksumVerificationAttemptsMetric : ObservabilityMetric
{
    public DownloadChecksumVerificationAttemptsMetric(long value, IReadOnlyDictionary<string, string> labels)
        : base(
            "drive_download_verifier_attempts_total",
            Version: 2,
            DateTime.UtcNow.ToUnixTimeSeconds(),
            new ObservabilityMetricProperties(value, labels))
    {
    }
}
