using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client;

public sealed class MoveLinkParameters
{
    [JsonPropertyName("ParentLinkID")]
    public required string ParentLinkId { get; init; }

    public required string NodePassphrase { get; init; }

    /// <summary>
    /// Node passphrase signature. Required when moving an anonymously created node. It must be signed using the <see cref="SignatureEmailAddress"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NodePassphraseSignature { get; init; }

    /// <summary>
    /// Node passphrase signature address. Required when moving an anonymously created node.
    /// </summary>
    [JsonPropertyName("SignatureEmail")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SignatureEmailAddress { get; init; }

    public required string Name { get; init; }

    [JsonPropertyName("Hash")]
    public required string NameHash { get; init; }

    [JsonPropertyName("NameSignatureEmail")]
    public required string NameSignatureEmailAddress { get; init; }

    /// <summary>
    /// Current name hash before move operation. Used to prevent race conditions.
    /// </summary>
    [JsonPropertyName("OriginalHash")]
    public string? OriginalNameHash { get; init; }
}
