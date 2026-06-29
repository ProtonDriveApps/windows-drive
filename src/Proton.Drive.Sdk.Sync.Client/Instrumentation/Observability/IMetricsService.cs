using System.Collections.Immutable;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability;

public interface IMetricsService
{
    void Start();
    void Stop();

    ImmutableList<ObservabilityMetric> GetMetrics(bool userHasAPaidPlan);
}
