using System.Collections.Immutable;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed record RevisionListResponse : ApiResponse
{
    private IImmutableList<RevisionHeader>? _revisions;

    public IImmutableList<RevisionHeader> Revisions
    {
        get => _revisions ??= ImmutableList<RevisionHeader>.Empty;
        init => _revisions = value;
    }
}
