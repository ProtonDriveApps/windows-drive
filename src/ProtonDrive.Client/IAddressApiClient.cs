using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Contracts;
using Refit;

namespace ProtonDrive.Client;

internal interface IAddressApiClient
{
    [Get("/v4/addresses")]
    [BearerAuthorizationHeader]
    Task<AddressListResponse> GetAddressesAsync(CancellationToken cancellationToken);
}
