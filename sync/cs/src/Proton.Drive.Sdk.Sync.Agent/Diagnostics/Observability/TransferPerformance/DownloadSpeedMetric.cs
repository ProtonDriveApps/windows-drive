using Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Observability.TransferPerformance;

public sealed record DownloadSpeedMetric : ObservabilityMetric
{
    public DownloadSpeedMetric(ObservabilityMetricProperties properties)
        : base("drive_download_speed_histogram", Version: 1, DateTime.UtcNow.ToUnixTimeSeconds(), properties)
    {
    }
}
