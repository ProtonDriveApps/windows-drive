using System.Text.Json.Serialization;
using Proton.Drive.Shared.Telemetry;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Telemetry;

public sealed class TelemetryEvents(IReadOnlyList<TelemetryEvent> events)
{
    [JsonPropertyName("EventInfo")]
    public IReadOnlyList<TelemetryEvent> Events { get; } = events;
}
