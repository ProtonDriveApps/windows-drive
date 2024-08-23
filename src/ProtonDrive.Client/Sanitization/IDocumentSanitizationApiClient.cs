using System.Threading;
using System.Threading.Tasks;
using Refit;

namespace ProtonDrive.Client.Sanitization;

public interface IDocumentSanitizationApiClient
{
    [Get("/sanitization/documents")]
    [BearerAuthorizationHeader]
    public Task<DocumentListResponse> GetLinksAsync(CancellationToken cancellationToken);
}
