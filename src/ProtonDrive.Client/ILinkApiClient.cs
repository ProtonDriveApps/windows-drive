using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Contracts;
using Refit;

namespace ProtonDrive.Client;

public interface ILinkApiClient
{
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
