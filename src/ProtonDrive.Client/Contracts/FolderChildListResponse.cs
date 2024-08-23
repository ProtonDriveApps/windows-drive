using System.Collections.Immutable;

namespace ProtonDrive.Client.Contracts;

public sealed record FolderChildListResponse : ApiResponse
{
    private IImmutableList<Link>? _links;

    public IImmutableList<Link> Links
    {
        get => _links ??= ImmutableList<Link>.Empty;
        init => _links = value;
    }
}
