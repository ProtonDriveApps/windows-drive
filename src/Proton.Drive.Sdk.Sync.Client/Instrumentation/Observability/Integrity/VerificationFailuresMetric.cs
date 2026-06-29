using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Integrity;

public sealed record VerificationFailuresMetric : ObservabilityMetric
{
    public VerificationFailuresMetric(long value, IReadOnlyDictionary<string, string> labels)
        : base(
            "drive_sdk_integrity_verification_errors_total",
            Version: 1,
            DateTime.UtcNow.ToUnixTimeSeconds(),
            new ObservabilityMetricProperties(value, labels))
    {
    }
}
