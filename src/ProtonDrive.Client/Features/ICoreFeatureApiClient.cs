using ProtonDrive.Client.Features.Contracts;
using Refit;

namespace ProtonDrive.Client.Features;

internal interface ICoreFeatureApiClient
{
    [Get("/v4/features")]
    [BearerAuthorizationHeader]
    Task<CoreFeatureListResponse> GetFeaturesAsync(CoreFeatureListParameters parameters, CancellationToken cancellationToken);
}
