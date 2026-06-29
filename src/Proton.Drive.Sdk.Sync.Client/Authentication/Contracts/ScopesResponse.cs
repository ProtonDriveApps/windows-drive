using System.Collections.Immutable;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Authentication.Contracts;

internal sealed record ScopesResponse : ApiResponse
{
    public IImmutableList<string> Scopes { get; init; } = ImmutableList<string>.Empty;
}
