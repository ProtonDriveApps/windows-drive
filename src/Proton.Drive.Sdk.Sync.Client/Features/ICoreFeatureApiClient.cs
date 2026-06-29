using Proton.Drive.Sdk.Sync.Client.Features.Contracts;
using Refit;

namespace Proton.Drive.Sdk.Sync.Client.Features;

internal interface ICoreFeatureApiClient
{
    [Get("/v4/features/{featureCode}")]
    [BearerAuthorizationHeader]
    Task<CoreFeatureResponse> GetFeatureAsync(string featureCode, CancellationToken cancellationToken);
}
