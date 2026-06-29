using System.Collections.Immutable;
using Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability;
using Proton.Drive.Sdk.Sync.Shared.SyncActivity;

namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Observability.TransferPerformance;

internal sealed class GenericTransferPerformanceMetricsFactory
{
    private readonly TransferPerformanceMonitors _monitors;

    public GenericTransferPerformanceMetricsFactory(TransferPerformanceMonitors monitors)
    {
        _monitors = monitors;
    }

    public ImmutableList<ObservabilityMetric> GetMetrics()
    {
        var transferSpeedMetrics = new List<ObservabilityMetric>();

        foreach (var (key, monitor) in _monitors)
        {
            var transferSpeed = monitor.GetTransferSpeedInKibibytesPerSecond();
            if (transferSpeed == null)
            {
                continue;
            }

            var properties = GetMetricProperties(transferSpeed.Value, key.Context, key.Pipeline);
            var metric = GetMetric(key.ActivityType, properties);

            if (metric != null)
            {
                transferSpeedMetrics.Add(metric);
            }
        }

        return transferSpeedMetrics.ToImmutableList();
    }

    public void Clear()
    {
        // We obtain but throw away values that accumulated before starting observing
        foreach (var (_, monitor) in _monitors)
        {
            monitor.GetTransferSpeed();
        }
    }

    private static ObservabilityMetric? GetMetric(SyncActivityType activityType, ObservabilityMetricProperties properties)
    {
        return activityType switch
        {
            SyncActivityType.Upload => new UploadSpeedMetric(properties),
            SyncActivityType.Download => new DownloadSpeedMetric(properties),
            _ => null,
        };
    }

    private static ObservabilityMetricProperties GetMetricProperties(double value, TransferContext context, TransferPipeline pipeline)
    {
        var labels = new Dictionary<string, string>
        {
            { "context", context.ToString().ToLowerInvariant() },
            { "pipeline", pipeline.ToString().ToLowerInvariant() },
        };

        return new ObservabilityMetricProperties(Value: (int)value, labels);
    }
}
