using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Albums.Contracts;

public sealed record AddedPhotoResponse : ApiResponse
{
    public AddedPhotoResponseDetails? Details { get; init; }
}
