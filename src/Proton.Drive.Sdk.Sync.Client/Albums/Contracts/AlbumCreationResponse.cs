using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Albums.Contracts;

public sealed record AlbumCreationResponse : ApiResponse
{
    private readonly AlbumShortDto? _album;

    public AlbumShortDto Album
    {
        get => _album ?? throw new ApiException("Album is not set");
        init => _album = value;
    }
}
