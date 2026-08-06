using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed record LinkResponse : ApiResponse
{
    public Link? Link { get; init; }
}
