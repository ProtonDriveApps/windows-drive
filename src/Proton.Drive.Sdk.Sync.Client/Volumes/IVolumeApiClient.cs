using Proton.Drive.Sdk.Sync.Client.Volumes.Contracts;
using Refit;

namespace Proton.Drive.Sdk.Sync.Client.Volumes;

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
