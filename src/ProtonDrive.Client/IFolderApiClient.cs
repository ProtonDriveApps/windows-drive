using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Contracts;
using Refit;

namespace ProtonDrive.Client;

public interface IFolderApiClient
{
    [Get("/shares/{shareId}/folders/{linkId}/children")]
    [BearerAuthorizationHeader]
    public Task<FolderChildListResponse> GetFolderChildrenAsync(
        string shareId,
        string linkId,
        FolderChildListParameters parameters,
        CancellationToken cancellationToken);

    [Post("/shares/{shareId}/folders")]
    [BearerAuthorizationHeader]
    public Task<FolderCreationResponse> CreateFolderAsync(string shareId, NodeCreationParameters parameters, CancellationToken cancellationToken);

    [Post("/shares/{shareId}/folders/{linkId}/trash_multiple")]
    [BearerAuthorizationHeader]
    public Task<MultipleResponses<FolderChildrenDeletionResponse>> MoveChildrenToTrashAsync(
        string shareId,
        string linkId,
        MultipleNodeActionParameters parameters,
        CancellationToken cancellationToken);

    [Post("/shares/{shareId}/folders/{linkId}/delete_multiple")]
    [BearerAuthorizationHeader]
    public Task<MultipleResponses<FolderChildrenDeletionResponse>> DeleteChildrenAsync(
        string shareId,
        string linkId,
        MultipleNodeActionParameters parameters,
        CancellationToken cancellationToken);
}
