using Proton.Drive.Sdk.Sync.Client.Features.Contracts;
using Refit;

namespace Proton.Drive.Sdk.Sync.Client.Features;

public interface IFeatureApiClient
{
    [Get("/v2/frontend")]
    [BearerAuthorizationHeader]
    Task<FeatureListResponse> GetFeaturesAsync(CancellationToken cancellationToken);
}
