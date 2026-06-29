using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Shared.Client;
using Refit;

namespace Proton.Drive.Sdk.Sync.Client;

public interface ILinkApiClient
{
    [Post("/shares/{shareId}/links/fetch_metadata")]
    [BearerAuthorizationHeader]
    Task<LinkResponseList> GetLinksAsync(string shareId, FetchLinksMetadataParameters parameters, CancellationToken cancellationToken);

    [Get("/shares/{shareId}/links/{linkId}")]
    [BearerAuthorizationHeader]
    Task<LinkResponse> GetLinkAsync(string shareId, string linkId, CancellationToken cancellationToken);

    [Put("/shares/{shareId}/links/{linkId}/move")]
    [BearerAuthorizationHeader]
    Task<ApiResponse> MoveLinkAsync(string shareId, string linkId, MoveLinkParameters parameters, CancellationToken cancellationToken);

    [Put("/shares/{shareId}/links/{linkId}/rename")]
    [BearerAuthorizationHeader]
    Task<ApiResponse> RenameLinkAsync(string shareId, string linkId, RenameLinkParameters parameters, CancellationToken cancellationToken);
}
