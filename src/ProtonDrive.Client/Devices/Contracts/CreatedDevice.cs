using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Devices.Contracts;

internal sealed record CreatedDevice
{
    [JsonPropertyName("DeviceID")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("ShareID")]
    public string ShareId { get; init; } = string.Empty;

    [JsonPropertyName("LinkID")]
    public string LinkId { get; init; } = string.Empty;
}
