using System.Collections.Immutable;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed record ShareListResponse : ApiResponse
{
    private IImmutableList<ShareListItem>? _shares;

    public IImmutableList<ShareListItem> Shares
    {
        get => _shares ??= ImmutableList<ShareListItem>.Empty;
        init => _shares = value;
    }
}
