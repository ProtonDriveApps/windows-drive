using Proton.Drive.Sdk.Sync.Client.Albums.Contracts;
using Proton.Drive.Sdk.Sync.Client.Photos.Contracts;
using Refit;

namespace Proton.Drive.Sdk.Sync.Client;

public interface IPhotoApiClient
{
    [Post("/v2/volumes/{volumeId}/links")]
    [BearerAuthorizationHeader]
    public Task<LinkResponseListV2> GetLinksDetailsAsync(string volumeId, LinkIdListParameter parameters, CancellationToken cancellationToken);

    [Get("/photos/volumes/{volumeId}/albums")]
    [BearerAuthorizationHeader]
    public Task<AlbumResponseList> GetAlbumsAsync(string volumeId, [Query, AliasAs("AnchorID")] string? anchorId, CancellationToken cancellationToken);

    [Post("/photos/volumes/{volumeId}/albums")]
    [BearerAuthorizationHeader]
    public Task<AlbumCreationResponse> CreateAlbumAsync(string volumeId, AlbumCreationParameters parameters, CancellationToken cancellationToken);

    [Post("/photos/volumes/{volumeId}/albums/{albumLinkId}/add-multiple")]
    [BearerAuthorizationHeader]
    public Task<AddedPhotoResponseList> AddPhotosToAlbumAsync(
        string volumeId,
        string albumLinkId,
        PhotoToAddListParameters parameters,
        CancellationToken cancellationToken);

    [Post("/volumes/{volumeId}/photos/duplicates")]
    [BearerAuthorizationHeader]
    public Task<PhotoDuplicationResponse> GetDuplicatesAsync(string volumeId, PhotoDuplicationParameters parameters, CancellationToken cancellationToken);
}
