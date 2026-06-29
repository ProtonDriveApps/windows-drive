using Proton.Drive.Sdk.Sync.Client.Contracts;
using Refit;

namespace Proton.Drive.Sdk.Sync.Client;

public interface IUserApiClient
{
    [Get("/v4/users")]
    [BearerAuthorizationHeader]
    Task<UserResponse> GetUserAsync(CancellationToken cancellationToken);

    [Get("/v4/organizations")]
    [BearerAuthorizationHeader]
    Task<OrganizationResponse> GetOrganizationAsync(CancellationToken cancellationToken);
}
