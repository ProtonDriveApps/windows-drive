using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public class NodeCreationParameters
{
    public string? Name { get; set; }

    [JsonPropertyName("ParentLinkID")]
    public string? ParentLinkId { get; set; }

    [JsonPropertyName("Hash")]
    public string? NameHash { get; set; }
    public string? NodePassphrase { get; set; }
    public string? NodePassphraseSignature { get; set; }

    [JsonPropertyName("SignatureAddress")]
    public string? SignatureEmailAddress { get; set; }
    public string? NodeKey { get; set; }
}
