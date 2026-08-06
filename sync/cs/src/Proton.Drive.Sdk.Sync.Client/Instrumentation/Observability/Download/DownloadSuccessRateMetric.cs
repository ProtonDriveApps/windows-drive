using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Download;

public sealed record DownloadSuccessRateMetric : ObservabilityMetric
{
    public DownloadSuccessRateMetric(long value, IReadOnlyDictionary<string, string> labels)
        : base(
            "drive_sdk_download_success_rate_total",
            Version: 1,
            DateTime.UtcNow.ToUnixTimeSeconds(),
            new ObservabilityMetricProperties(value, labels))
    {
    }
}
