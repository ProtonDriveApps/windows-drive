using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Integrity;

public sealed record IntegrityFailuresImpactedUserMetric : ObservabilityMetric
{
    public IntegrityFailuresImpactedUserMetric(long value, IReadOnlyDictionary<string, string> labels)
        : base(
            "drive_sdk_integrity_erroring_users_total",
            Version: 1,
            DateTime.UtcNow.ToUnixTimeSeconds(),
            new ObservabilityMetricProperties(value, labels))
    {
    }
}
