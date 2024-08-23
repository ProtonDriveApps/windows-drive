using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed class VolumeRootShare
{
    [JsonPropertyName("ShareID")]
    public required string Id { get; set; }

    [JsonPropertyName("LinkID")]
    public required string LinkId { get; set; }
}
