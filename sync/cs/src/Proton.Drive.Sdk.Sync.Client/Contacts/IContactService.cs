namespace Proton.Drive.Sdk.Sync.Client.Contacts;

public interface IContactService
{
    Task<string?> GetDisplayNameByEmailAddressAsync(string email, CancellationToken cancellationToken);
}
