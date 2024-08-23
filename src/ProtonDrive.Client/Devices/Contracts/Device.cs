using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Devices.Contracts;

internal sealed record Device
{
    [JsonPropertyName("DeviceID")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("VolumeID")]
    public string VolumeId { get; init; } = string.Empty;

    [JsonPropertyName("Type")]
    public DevicePlatform Platform { get; init; }

    [JsonPropertyName("SyncState")]
    [JsonConverter(typeof(BooleanToIntegerJsonConverter))]
    public bool IsSynchronizationEnabled { get; init; }
}
