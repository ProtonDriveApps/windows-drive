using System.Text.Json.Serialization;

namespace Proton.Drive.Shared.Telemetry;

public sealed record TelemetryEvent(
    string MeasurementGroup,
    [property: JsonPropertyName("Event")] string EventName,
    IReadOnlyDictionary<string, double> Values,
    IReadOnlyDictionary<string, string> Dimensions);
