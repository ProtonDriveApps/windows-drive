using System.Collections.Immutable;

namespace ProtonDrive.Client.Contracts;

public sealed record ShareListResponse : ApiResponse
{
    private IImmutableList<ShareListItem>? _shares;

    public IImmutableList<ShareListItem> Shares
    {
        get => _shares ??= ImmutableList<ShareListItem>.Empty;
        init => _shares = value;
    }
}
