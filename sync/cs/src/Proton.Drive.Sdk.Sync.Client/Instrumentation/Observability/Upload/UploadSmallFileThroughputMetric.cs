using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Upload;

public sealed record UploadSmallFileThroughputMetric : ObservabilityMetric
{
    public UploadSmallFileThroughputMetric(long value, IReadOnlyDictionary<string, string> labels)
        : base(
            "drive_sdk_upload_small_file_throughput_histogram",
            Version: 1,
            DateTime.UtcNow.ToUnixTimeSeconds(),
            new ObservabilityMetricProperties(value, labels))
    {
    }
}
