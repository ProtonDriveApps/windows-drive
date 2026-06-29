using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed record Thumbnail(
    [property: JsonPropertyName("ThumbnailID")] string Id,
    int Type,
    int Size);
