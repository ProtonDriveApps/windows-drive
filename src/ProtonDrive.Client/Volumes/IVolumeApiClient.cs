using ProtonDrive.Client.Volumes.Contracts;
using Refit;

namespace ProtonDrive.Client.Volumes;

internal interface IVolumeApiClient
{
    [Get("/volumes/{volumeId}")]
    [BearerAuthorizationHeader]
    Task<VolumeResponse> GetVolumeAsync(string volumeId, CancellationToken cancellationToken);

    [Get("/volumes")]
    [BearerAuthorizationHeader]
    Task<VolumeListResponse> GetVolumesAsync(CancellationToken cancellationToken);

    [Post("/volumes")]
    [BearerAuthorizationHeader]
    Task<VolumeCreationResponse> CreateMainVolumeAsync(VolumeCreationParameters parameters, CancellationToken cancellationToken);

    [Post("/photos/volumes")]
    [BearerAuthorizationHeader]
    Task<VolumeCreationResponse> CreatePhotoVolumeAsync(PhotoVolumeCreationParameters parameters, CancellationToken cancellationToken);
}
