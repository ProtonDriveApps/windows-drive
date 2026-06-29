using Proton.Drive.Sdk.Sync.Client.Contracts;
using Refit;

namespace Proton.Drive.Sdk.Sync.Client;

internal interface IAddressApiClient
{
    [Get("/v4/addresses")]
    [BearerAuthorizationHeader]
    Task<AddressListResponse> GetAddressesAsync(CancellationToken cancellationToken);
}
