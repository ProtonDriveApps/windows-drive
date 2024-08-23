using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed class AddressKeyDto
{
    [JsonPropertyName("ID")]
    public string Id { get; set; } = string.Empty;

    public int Version { get; set; }

    public string PrivateKey { get; set; } = string.Empty;

    public string? Token { get; set; }
    public string? Signature { get; set; }

    [JsonPropertyName("Primary")]
    [JsonConverter(typeof(BooleanToIntegerJsonConverter))]
    public bool IsPrimary { get; set; }

    [JsonPropertyName("Active")]
    [JsonConverter(typeof(BooleanToIntegerJsonConverter))]
    public bool IsActive { get; set; }

    public AddressKeyFlags Flags { get; set; }
}
