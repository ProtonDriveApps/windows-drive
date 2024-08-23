using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Devices;

public sealed record Device
{
    public string Id { get; init; } = string.Empty;
    public string VolumeId { get; init; } = string.Empty;

    [JsonPropertyName("Type")]
    public DevicePlatform Platform { get; init; }

    public string Name { get; init; } = string.Empty;
    public string ShareId { get; init; } = string.Empty;
    public string LinkId { get; init; } = string.Empty;
    public bool IsSynchronizationEnabled { get; init; }
}
