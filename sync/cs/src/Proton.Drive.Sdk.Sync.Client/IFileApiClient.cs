using Proton.Drive.Shared.Client;
using Refit;

namespace Proton.Drive.Sdk.Sync.Client;

public interface IFileApiClient
{
    [Delete("/shares/{shareId}/files/{linkId}/revisions/{revisionId}")]
    [BearerAuthorizationHeader]
    public Task<ApiResponse> DeleteRevisionAsync(
        string shareId,
        string linkId,
        string revisionId,
        CancellationToken cancellationToken);
}
