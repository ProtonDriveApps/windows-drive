using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Shared.Client;
using Refit;

namespace Proton.Drive.Sdk.Sync.Client;

public interface IFileRevisionUpdateApiClient
{
    [Put("/shares/{shareId}/files/{linkId}/revisions/{revisionId}")]
    [BearerAuthorizationHeader]
    public Task<ApiResponse> UpdateRevisionAsync(
        string shareId,
        string linkId,
        string revisionId,
        RevisionUpdateParameters parameters,
        CancellationToken cancellationToken);
}
