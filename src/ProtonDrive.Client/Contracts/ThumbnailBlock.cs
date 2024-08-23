using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record ThumbnailBlock(
    [property: JsonPropertyName("ThumbnailID")] string Id,
    [property: JsonPropertyName("BareURL")] string BareUrl,
    string Token);
