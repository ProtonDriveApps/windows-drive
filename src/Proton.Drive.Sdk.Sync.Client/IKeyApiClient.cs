using Proton.Drive.Sdk.Sync.Client.Contracts;
using Refit;

namespace Proton.Drive.Sdk.Sync.Client;

public interface IKeyApiClient
{
    [Get("/v4/keys/all?InternalOnly=1")]
    [BearerAuthorizationHeader]
    Task<PublicKeyListResponse> GetActivePublicKeysAsync([AliasAs("Email")] string emailAddress, CancellationToken cancellationToken);

    [Get("/v4/keys/salts")]
    [BearerAuthorizationHeader]
    Task<KeySaltListResponse> GetKeySaltsAsync(CancellationToken cancellationToken);
}
