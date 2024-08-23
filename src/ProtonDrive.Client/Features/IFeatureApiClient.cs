using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Features.Contracts;
using Refit;

namespace ProtonDrive.Client.Features;

public interface IFeatureApiClient
{
    [Get("/v2/frontend")]
    [BearerAuthorizationHeader]
    Task<FeatureListResponse> GetFeaturesAsync(CancellationToken cancellationToken);
}
