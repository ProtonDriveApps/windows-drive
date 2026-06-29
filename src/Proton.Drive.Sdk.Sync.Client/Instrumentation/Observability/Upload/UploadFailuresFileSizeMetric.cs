using System.Collections.Immutable;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Upload;

public sealed record UploadFailuresFileSizeMetric : ObservabilityMetric
{
    public UploadFailuresFileSizeMetric(long value)
        : base(
            "drive_sdk_upload_errors_file_size_histogram",
            Version: 1,
            DateTime.UtcNow.ToUnixTimeSeconds(),
            new ObservabilityMetricProperties(value, ImmutableDictionary<string, string>.Empty))
    {
    }
}
