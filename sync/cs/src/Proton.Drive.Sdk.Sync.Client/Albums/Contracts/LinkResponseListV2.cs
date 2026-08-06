using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Albums.Contracts;

public sealed record LinkResponseListV2 : ApiResponse
{
    public required IReadOnlyList<LinkResponseV2> Links { get; init; }
}
