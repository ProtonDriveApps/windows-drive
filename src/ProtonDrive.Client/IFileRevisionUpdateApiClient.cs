using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Contracts;
using Refit;

namespace ProtonDrive.Client;

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
