using System.Collections.Immutable;
using ProtonDrive.Client.Contracts;

namespace ProtonDrive.Client.Volumes;

public sealed record ThumbnailListResponse : ApiResponse
{
    private IImmutableList<ThumbnailBlock>? _thumbnails;

    public IImmutableList<ThumbnailBlock> Thumbnails
    {
        get => _thumbnails ??= ImmutableList<ThumbnailBlock>.Empty;
        init => _thumbnails = value;
    }
}
