using ProtonDrive.Client.Instrumentation.Observability;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.App.Instrumentation.Observability;

public sealed record LegacyUploadSuccessRateMetric : ObservabilityMetric
{
    public LegacyUploadSuccessRateMetric(ObservabilityMetricProperties properties)
        : base("drive_upload_success_rate_total", Version: 2, DateTime.UtcNow.ToUnixTimeSeconds(), properties)
    {
    }
}
