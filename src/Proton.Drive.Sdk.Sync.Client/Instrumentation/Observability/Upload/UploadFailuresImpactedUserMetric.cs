using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Upload;

public sealed record UploadFailuresImpactedUserMetric : ObservabilityMetric
{
    public UploadFailuresImpactedUserMetric(long value, IReadOnlyDictionary<string, string> labels)
        : base(
            "drive_sdk_upload_erroring_users_total",
            Version: 1,
            DateTime.UtcNow.ToUnixTimeSeconds(),
            new ObservabilityMetricProperties(value, labels))
    {
    }
}
