using System.Collections.Immutable;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Download;

public sealed record DownloadFailuresFileSizeMetric : ObservabilityMetric
{
    public DownloadFailuresFileSizeMetric(long value)
        : base(
            "drive_sdk_download_errors_file_size_histogram",
            Version: 1,
            DateTime.UtcNow.ToUnixTimeSeconds(),
            new ObservabilityMetricProperties(value, ImmutableDictionary<string, string>.Empty))
    {
    }
}
