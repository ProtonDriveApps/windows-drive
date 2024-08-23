using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed class UserKey
{
    [JsonPropertyName("ID")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("Active")]
    [JsonConverter(typeof(BooleanToIntegerJsonConverter))]
    public bool IsActive { get; set; }

    [JsonPropertyName("Primary")]
    [JsonConverter(typeof(BooleanToIntegerJsonConverter))]
    public bool IsPrimary { get; set; }

    public int Version { get; set; }
    public string PrivateKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
}
