using ProtonDrive.Client.Instrumentation.Observability;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.App.Instrumentation.Observability;

public sealed record LegacyDownloadSuccessRateMetric : ObservabilityMetric
{
    public LegacyDownloadSuccessRateMetric(ObservabilityMetricProperties properties)
        : base("drive_download_success_rate_total", Version: 1, DateTime.UtcNow.ToUnixTimeSeconds(), properties)
    {
    }
}
