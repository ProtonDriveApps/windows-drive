using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Contracts;
using Refit;

namespace ProtonDrive.Client.Shares;

public interface IShareApiClient
{
    [Get("/shares")]
    [BearerAuthorizationHeader]
    Task<ShareListResponse> GetSharesAsync(CancellationToken cancellationToken);

    [Get("/shares/{id}")]
    [BearerAuthorizationHeader]
    Task<ShareResponse> GetShareAsync(string id, CancellationToken cancellationToken);

    [Delete("/shares/{id}")]
    [BearerAuthorizationHeader]
    Task<ApiResponse> DeleteAsync(string id, CancellationToken cancellationToken);

    [Get("/v2/sharedwithme")]
    [BearerAuthorizationHeader]
    Task<SharedWithMeItemListResponse> GetSharedWithMeItemsAsync([Query] string? anchorId, CancellationToken cancellationToken);

    [Delete("/v2/shares/{id}/members/{memberId}")]
    [BearerAuthorizationHeader]
    Task<SharedWithMeItemListResponse> RemoveMemberAsync(string id, string memberId, CancellationToken cancellationToken);
}
