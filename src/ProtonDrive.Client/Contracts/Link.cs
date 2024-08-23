using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record Link
{
    [JsonPropertyName("LinkID")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("ParentLinkID")]
    public string? ParentId { get; init; }

    public LinkType Type { get; init; }
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("NameSignatureEmail")]
    public string? NameSignatureEmailAddress { get; init; }

    [JsonPropertyName("Hash")]
    public string? NameHash { get; init; }

    public LinkState State { get; init; }
    public long? ExpirationTime { get; init; }
    public long Size { get; init; }

    [JsonPropertyName("MIMEType")]
    public string? MediaType { get; init; }

    public int Attributes { get; init; }
    public string NodeKey { get; init; } = string.Empty;
    public string NodePassphrase { get; init; } = string.Empty;
    public string NodePassphraseSignature { get; init; } = string.Empty;

    [JsonPropertyName("SignatureEmail")]
    public string SignatureEmailAddress { get; init; } = string.Empty;

    [JsonPropertyName("CreateTime")]
    public long CreationTime { get; init; }

    [JsonPropertyName("ModifyTime")]
    public long ModificationTime { get; init; }

    [JsonPropertyName("Trashed")]
    public long? DeletionTime { get; init; }

    public FileProperties? FileProperties { get; init; }
    public FolderProperties? FolderProperties { get; init; }

    public LinkSharingDetails? SharingDetails { get; init; }

    [JsonPropertyName("XAttr")]
    public string? ExtendedAttributes { get; init; }
}
