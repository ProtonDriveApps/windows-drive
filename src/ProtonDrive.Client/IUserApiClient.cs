using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Contracts;
using Refit;

namespace ProtonDrive.Client;

public interface IUserApiClient
{
    [Get("/v4/users")]
    [BearerAuthorizationHeader]
    Task<UserResponse> GetUserAsync(CancellationToken cancellationToken);

    [Get("/v4/organizations")]
    [BearerAuthorizationHeader]
    Task<OrganizationResponse> GetOrganizationAsync(CancellationToken cancellationToken);
}
