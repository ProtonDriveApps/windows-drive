namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability;

public sealed record ObservabilityMetricProperties(long Value, IReadOnlyDictionary<string, string> Labels);
