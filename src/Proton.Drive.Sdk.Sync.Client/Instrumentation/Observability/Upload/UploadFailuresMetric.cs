using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Upload;

public sealed record UploadFailuresMetric : ObservabilityMetric
{
    public UploadFailuresMetric(long value, IReadOnlyDictionary<string, string> labels)
        : base(
            "drive_sdk_upload_errors_total",
            Version: 1,
            DateTime.UtcNow.ToUnixTimeSeconds(),
            new ObservabilityMetricProperties(value, labels))
    {
    }
}
