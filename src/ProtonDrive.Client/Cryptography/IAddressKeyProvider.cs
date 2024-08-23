using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Proton.Security.Cryptography.Abstractions;

namespace ProtonDrive.Client.Cryptography;

public interface IAddressKeyProvider
{
    Task<Address> GetAddressAsync(string addressId, CancellationToken cancellationToken);
    Task<Address> GetUserDefaultAddressAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AddressKey>> GetAddressKeysAsync(IReadOnlyCollection<string> addressIds, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AddressKey>> GetAddressKeysForEmailAddressesAsync(IReadOnlyCollection<string> emailAddresses, CancellationToken cancellationToken);
    Task<IReadOnlyList<PublicPgpKey>> GetPublicKeysForEmailAddressAsync(string emailAddress, CancellationToken cancellationToken);
    void ClearUserAddressesCache();
}
