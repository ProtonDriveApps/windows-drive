using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Telemetry;

public sealed class TelemetryEvents(IReadOnlyList<TelemetryEvent> events)
{
    [JsonPropertyName("EventInfo")]
    public IReadOnlyList<TelemetryEvent> Events { get; } = events;
}
