using System;
using System.Text.Json.Serialization;
using ProtonDrive.Shared.Text.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record Thumbnail(
    [property: JsonPropertyName("ThumbnailID")] string Id,
    int Type,
    [property: JsonPropertyName("BaseURL"), JsonConverter(typeof(Base64JsonConverter))] ReadOnlyMemory<byte> Hash,
    int Size);
