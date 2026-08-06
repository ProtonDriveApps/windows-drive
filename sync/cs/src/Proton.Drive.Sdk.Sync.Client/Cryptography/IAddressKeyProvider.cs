using Proton.Cryptography.Pgp;

namespace Proton.Drive.Sdk.Sync.Client.Cryptography;

public interface IAddressKeyProvider
{
    Task<Address> GetAddressAsync(string addressId, CancellationToken cancellationToken);
    Task<Address> GetUserDefaultAddressAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AddressKey>> GetAddressKeysAsync(IReadOnlyCollection<string> addressIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<PgpPublicKey>> GetPublicKeysForEmailAddressAsync(string emailAddress, CancellationToken cancellationToken);
    void ClearUserAddressesCache();
}
