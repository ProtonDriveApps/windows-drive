using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record BlockRequestResponse : ApiResponse
{
    private IImmutableList<UploadUrl>? _uploadUrls;

    [JsonPropertyName("UploadLinks")]
    public IImmutableList<UploadUrl> UploadUrls
    {
        get => _uploadUrls ??= ImmutableList<UploadUrl>.Empty;
        init => _uploadUrls = value;
    }

    [JsonPropertyName("ThumbnailLink")]
    public UploadUrl? ThumbnailUrl { get; init; }
}
