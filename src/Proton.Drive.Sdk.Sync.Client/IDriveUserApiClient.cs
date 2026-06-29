using Proton.Drive.Sdk.Sync.Client.Contracts;
using Refit;

namespace Proton.Drive.Sdk.Sync.Client;

public interface IDriveUserApiClient
{
    [Get("/me/active")]
    [BearerAuthorizationHeader]
    public Task<ActivityResponse> GetIsActiveAsync(CancellationToken cancellationToken);
}
