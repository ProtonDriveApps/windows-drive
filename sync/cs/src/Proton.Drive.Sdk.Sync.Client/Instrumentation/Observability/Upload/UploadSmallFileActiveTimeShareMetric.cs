using System.Collections.Immutable;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Upload;

public sealed record UploadSmallFileActiveTimeShareMetric : ObservabilityMetric
{
    public UploadSmallFileActiveTimeShareMetric(long value)
        : base(
            "drive_sdk_upload_small_file_active_time_share_histogram",
            Version: 1,
            DateTime.UtcNow.ToUnixTimeSeconds(),
            new ObservabilityMetricProperties(value, ImmutableDictionary<string, string>.Empty))
    {
    }
}
