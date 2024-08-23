using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Contracts;
using Refit;

namespace ProtonDrive.Client;

public interface IDriveUserApiClient
{
    [Get("/me/active")]
    [BearerAuthorizationHeader]
    public Task<ActivityResponse> GetIsActiveAsync(CancellationToken cancellationToken);
}
