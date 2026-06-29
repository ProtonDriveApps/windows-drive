namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability;

public sealed class ObservabilityMetrics(IReadOnlyList<ObservabilityMetric> metrics)
{
    public IReadOnlyList<ObservabilityMetric> Metrics { get; } = metrics;
}
