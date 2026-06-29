using System.Collections.Immutable;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed record FolderChildListResponse : ApiResponse
{
    private IImmutableList<Link>? _links;

    public IImmutableList<Link> Links
    {
        get => _links ??= ImmutableList<Link>.Empty;
        init => _links = value;
    }
}
