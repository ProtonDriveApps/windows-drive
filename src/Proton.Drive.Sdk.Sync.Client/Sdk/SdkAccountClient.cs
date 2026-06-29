using Proton.Cryptography.Pgp;
using Proton.Drive.Sdk;
using Proton.Drive.Sdk.Sync.Client.Cryptography;
using Proton.Sdk.Addresses;
using AddressId = Proton.Sdk.Addresses.AddressId;

namespace Proton.Drive.Sdk.Sync.Client.Sdk;

internal sealed class SdkAccountClient(IAddressKeyProvider addressKeyProvider) : IAccountClient
{
    private readonly IAddressKeyProvider _addressKeyProvider = addressKeyProvider;

    public async ValueTask<Proton.Sdk.Addresses.Address> GetAddressAsync(AddressId addressId, CancellationToken cancellationToken)
    {
        var address = await _addressKeyProvider.GetAddressAsync(addressId.ToString(), cancellationToken).ConfigureAwait(false);
        return ConvertToSdkAddress(address);
    }

    public async ValueTask<Proton.Sdk.Addresses.Address> GetDefaultAddressAsync(CancellationToken cancellationToken)
    {
        var address = await _addressKeyProvider.GetUserDefaultAddressAsync(cancellationToken).ConfigureAwait(false);
        return ConvertToSdkAddress(address);
    }

    public async ValueTask<PgpPrivateKey> GetAddressPrimaryPrivateKeyAsync(AddressId addressId, CancellationToken cancellationToken)
    {
        var address = await _addressKeyProvider.GetAddressAsync(addressId.ToString(), cancellationToken).ConfigureAwait(false);
        return address.Keys[address.PrimaryKeyIndex].PrivateKey;
    }

    public async ValueTask<IReadOnlyList<PgpPrivateKey>> GetAddressPrivateKeysAsync(AddressId addressId, CancellationToken cancellationToken)
    {
        var address = await _addressKeyProvider.GetAddressAsync(addressId.ToString(), cancellationToken).ConfigureAwait(false);
        return address.Keys.Select(x => x.PrivateKey).ToList().AsReadOnly();
    }

    public ValueTask<IReadOnlyList<PgpPublicKey>> GetAddressPublicKeysAsync(string emailAddress, CancellationToken cancellationToken)
    {
        return new ValueTask<IReadOnlyList<PgpPublicKey>>(_addressKeyProvider.GetPublicKeysForEmailAddressAsync(emailAddress, cancellationToken));
    }

    private static Proton.Sdk.Addresses.Address ConvertToSdkAddress(Cryptography.Address address)
    {
        var addressId = (AddressId)address.Id;

        return new Proton.Sdk.Addresses.Address(
            addressId,
            order: 0,
            address.EmailAddress,
            (AddressStatus)address.Status,
            address.Keys.Select((x, i) => ConvertToSdkAddressKey(addressId, x, i == address.PrimaryKeyIndex)).ToList().AsReadOnly(),
            address.PrimaryKeyIndex);
    }

    private static Proton.Sdk.Addresses.AddressKey ConvertToSdkAddressKey(AddressId addressId, Cryptography.AddressKey addressKey, bool isPrimary)
    {
        return new Proton.Sdk.Addresses.AddressKey(
            addressId,
            (AddressKeyId)addressKey.Id,
            isPrimary,
            isActive: true, // Not active keys are not returned by the IAddressKeyProvider
            addressKey.IsAllowedForEncryption,
            isAllowedForVerification: addressKey.IsAllowedForEncryption);
    }
}
