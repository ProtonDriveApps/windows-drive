using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record UploadUrl
{
    public string Token { get; init; } = string.Empty;

    [JsonPropertyName("URL")]
    public string Value { get; init; } = string.Empty;
}
