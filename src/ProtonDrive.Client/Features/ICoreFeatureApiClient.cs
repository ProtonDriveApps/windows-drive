using ProtonDrive.Client.Features.Contracts;
using Refit;

namespace ProtonDrive.Client.Features;

internal interface ICoreFeatureApiClient
{
    [Get("/v4/features/{featureCode}")]
    [BearerAuthorizationHeader]
    Task<CoreFeatureResponse> GetFeatureAsync(string featureCode, CancellationToken cancellationToken);
}
