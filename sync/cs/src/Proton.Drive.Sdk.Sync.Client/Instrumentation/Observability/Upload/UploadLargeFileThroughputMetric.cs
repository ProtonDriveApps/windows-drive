using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Upload;

public sealed record UploadLargeFileThroughputMetric : ObservabilityMetric
{
    public UploadLargeFileThroughputMetric(long value, IReadOnlyDictionary<string, string> labels)
        : base(
            "drive_sdk_upload_large_file_throughput_histogram",
            Version: 1,
            DateTime.UtcNow.ToUnixTimeSeconds(),
            new ObservabilityMetricProperties(value, labels))
    {
    }
}
