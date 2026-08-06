using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability;

public abstract record ObservabilityMetric(
    string Name,
    int Version,
    long Timestamp,
    [property: JsonPropertyName("Data")] ObservabilityMetricProperties Properties);
