using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Sanitization;

public sealed record DocumentIdentity(
    [property: JsonPropertyName("VolumeID")] string VolumeId,
    [property: JsonPropertyName("LinkID")] string LinkId);
