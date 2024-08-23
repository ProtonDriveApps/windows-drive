using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed class KeySalt
{
    [JsonPropertyName("ID")]
    public string KeyId { get; set; } = string.Empty;

    [JsonPropertyName("KeySalt")]
    public string? Value { get; set; } = string.Empty;
}
