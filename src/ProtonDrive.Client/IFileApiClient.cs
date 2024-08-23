using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Contracts;
using Refit;

namespace ProtonDrive.Client;

public interface IFileApiClient
{
    [Post("/shares/{shareId}/files")]
    [BearerAuthorizationHeader]
    public Task<FileCreationResponse> CreateFileAsync(string shareId, FileCreationParameters parameters, CancellationToken cancellationToken);

    [Post("/shares/{shareId}/files/{linkId}/revisions")]
    [BearerAuthorizationHeader]
    public Task<RevisionCreationResponse> CreateRevisionAsync(
        string shareId,
        string linkId,
        FileRevisionCreationParameters fileCreationParameters,
        CancellationToken cancellationToken);

    [Get("/shares/{shareId}/files/{linkId}/revisions/{revisionId}")]
    [BearerAuthorizationHeader]
    public Task<RevisionResponse> GetRevisionAsync(
        string shareId,
        string linkId,
        string revisionId,
        [AliasAs("FromBlockIndex")] int fromBlockIndex,
        [AliasAs("PageSize")] int pageSize,
        [AliasAs("NoBlockUrls")] bool noBlockUrls,
        CancellationToken cancellationToken);

    [Delete("/shares/{shareId}/files/{linkId}/revisions/{revisionId}")]
    [BearerAuthorizationHeader]
    public Task<ApiResponse> DeleteRevisionAsync(
        string shareId,
        string linkId,
        string revisionId,
        CancellationToken cancellationToken);

    [Post("/blocks")]
    [BearerAuthorizationHeader]
    internal Task<BlockRequestResponse> RequestBlockUploadAsync(BlockUploadRequestParameters parameters, CancellationToken cancellationToken);
}
