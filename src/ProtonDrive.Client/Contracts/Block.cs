using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed class Block
{
    public int Index { get; set; }

    [JsonPropertyName("URL")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("EncSignature")]
    public string? EncryptedSignature { get; set; }

    [JsonPropertyName("SignatureEmail")]
    public string SignatureEmailAddress { get; set; } = string.Empty;
}
