using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public class RevisionHeader
{
    private IImmutableList<Thumbnail>? _thumbnails;

    [JsonPropertyName("ID")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("CreateTime")]
    public long CreationTime { get; set; }

    public long Size { get; set; }

    [JsonPropertyName("SignatureEmail")]
    public string? SignatureEmailAddress { get; set; }

    public bool ChecksumVerified { get; init; }

    public string ManifestSignature { get; set; } = string.Empty;

    public RevisionState State { get; set; }

    public IImmutableList<Thumbnail> Thumbnails
    {
        get => _thumbnails ??= ImmutableList.Create<Thumbnail>();
        init => _thumbnails = value;
    }
}
