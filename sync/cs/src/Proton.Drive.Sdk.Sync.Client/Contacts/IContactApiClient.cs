using Proton.Drive.Sdk.Sync.Client.Contacts.Contracts;
using Refit;

namespace Proton.Drive.Sdk.Sync.Client.Contacts;

public interface IContactApiClient
{
    [Get("/v4/emails")]
    [BearerAuthorizationHeader]
    Task<ContactListResponse> GetContactsAsync(CancellationToken cancellationToken);
}
