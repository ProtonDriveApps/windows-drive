using System.Text.Json.Serialization;
using ProtonDrive.Client.Contracts;

namespace ProtonDrive.Client.Albums.Contracts;

public sealed record LinkDto
{
    [JsonPropertyName("LinkID")]
    public required string Id { get; init; }

    [JsonPropertyName("ParentLinkID")]
    public string? ParentId { get; init; }

    public required LinkType Type { get; init; }
    public required LinkState State { get; init; }

    [JsonPropertyName("CreateTime")]
    public required long CreationTime { get; init; }

    [JsonPropertyName("ModifyTime")]
    public required long ModificationTime { get; init; }

    [JsonPropertyName("Trashed")]
    public long? DeletionTime { get; init; }

    public required string Name { get; init; }
    public string? NameHash { get; init; }
    public required string NodeKey { get; init; } = string.Empty;
    public required string NodePassphrase { get; init; } = string.Empty;
    public required string NodePassphraseSignature { get; init; } = string.Empty;

    [JsonPropertyName("SignatureEmail")]
    public string? SignatureEmailAddress { get; init; }

    [JsonPropertyName("NameSignatureEmail")]
    public string? NameSignatureEmailAddress { get; init; }
}
