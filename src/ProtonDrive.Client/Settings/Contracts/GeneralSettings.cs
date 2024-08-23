using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Settings.Contracts;

public sealed record GeneralSettings
{
    [JsonPropertyName("Telemetry")]
    [JsonConverter(typeof(BooleanToIntegerJsonConverter))]
    public bool IsTelemetryEnabled { get; init; }

    [JsonPropertyName("CrashReports")]
    [JsonConverter(typeof(BooleanToIntegerJsonConverter))]
    public bool IsSendingCrashReportsEnabled { get; init; }
}
