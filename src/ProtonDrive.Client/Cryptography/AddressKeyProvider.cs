using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Proton.Security.Cryptography;
using Proton.Security.Cryptography.Abstractions;
using ProtonDrive.Client.Authentication;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Shared.Caching;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.Client.Cryptography;

internal sealed class AddressKeyProvider : IAddressKeyProvider
{
    private static readonly object UserAddressesCacheKey = new();

    private readonly IAddressApiClient _addressApiClient;
    private readonly IUserClient _userClient;
    private readonly IKeyApiClient _keyApiClient;
    private readonly IKeyPassphraseProvider _keyPassphraseProvider;
    private readonly IPgpTransformerFactory _pgpTransformerFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AddressKeyProvider> _logger;

    private readonly SemaphoreSlim _cacheSemaphore = new(1);

    public AddressKeyProvider(
        IAddressApiClient addressApiClient,
        IUserClient userClient,
        IKeyApiClient keyApiClient,
        IKeyPassphraseProvider keyPassphraseProvider,
        IPgpTransformerFactory pgpTransformerFactory,
        IMemoryCache cache,
        ILogger<AddressKeyProvider> logger)
    {
        _addressApiClient = addressApiClient;
        _userClient = userClient;
        _keyApiClient = keyApiClient;
        _keyPassphraseProvider = keyPassphraseProvider;
        _pgpTransformerFactory = pgpTransformerFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Address> GetAddressAsync(string addressId, CancellationToken cancellationToken)
    {
        var addresses = await GetUserAddressesAsync(cancellationToken).ConfigureAwait(false);

        if (!addresses.TryGetValue(addressId, out var address))
        {
            throw new UnknownUserAddressException(addressId);
        }

        return address;
    }

    public async Task<Address> GetUserDefaultAddressAsync(CancellationToken cancellationToken)
    {
        var userAddresses = await GetUserAddressesAsync(cancellationToken).ConfigureAwait(false);
        var defaultAddress = GetDefaultAddress(userAddresses.Values);

        return defaultAddress;
    }

    public async Task<IReadOnlyCollection<AddressKey>> GetAddressKeysAsync(IReadOnlyCollection<string> addressIds, CancellationToken cancellationToken)
    {
        var addresses = await GetUserAddressesAsync(cancellationToken).ConfigureAwait(false);

        var addressKeysQuery =
            from addressId in addressIds
            let address = addresses.GetValueOrDefault(addressId)
            where address is not null
            select address.Keys into addressKeys
            from addressKey in addressKeys
            select addressKey;

        return addressKeysQuery.ToList().AsReadOnly();
    }

    public async Task<IReadOnlyCollection<AddressKey>> GetAddressKeysForEmailAddressesAsync(
        IReadOnlyCollection<string> emailAddresses,
        CancellationToken cancellationToken)
    {
        var addresses = await GetUserAddressesAsync(cancellationToken).ConfigureAwait(false);

        var addressKeysQuery =
            from emailAddress in emailAddresses
            join address in addresses.Values
                on emailAddress equals address.EmailAddress
            select address.Keys into keys
            from key in keys
            select key;

        return addressKeysQuery.ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<PublicPgpKey>> GetPublicKeysForEmailAddressAsync(string emailAddress, CancellationToken cancellationToken)
    {
        return await _cache.GetOrExclusivelyCreateAsync(
            new PublicKeysCacheKey(emailAddress),
            async () =>
            {
                var publicKeysResponse = await _keyApiClient.GetActivePublicKeysAsync(emailAddress, cancellationToken).ThrowOnFailure()
                    .ConfigureAwait(false);

                var publicKeys = new List<PublicPgpKey>(publicKeysResponse.Address.Keys.Count);
                publicKeys.AddRange(
                    publicKeysResponse.Address.Keys
                        .Where(keyEntry => (keyEntry.Flags & PublicKeyFlags.IsNotCompromised) != 0)
                        .Select(entry => PublicPgpKey.FromArmored(entry.PublicKey)));
                return publicKeys.AsReadOnly();
            },
            _cacheSemaphore,
            cancellationToken).ConfigureAwait(false);
    }

    public void ClearUserAddressesCache()
    {
        _cache.Remove(UserAddressesCacheKey);
    }

    private static Address GetDefaultAddress(IEnumerable<Address> addresses)
    {
        return addresses.First();
    }

    private async Task<IReadOnlyDictionary<string, Address>> GetUserAddressesAsync(CancellationToken cancellationToken)
    {
        return await _cache.GetOrExclusivelyCreateAsync(
            UserAddressesCacheKey,
            async () =>
            {
                var addressListResponse = await _addressApiClient.GetAddressesAsync(cancellationToken).ThrowOnFailure().ConfigureAwait(false);

                var addresses = addressListResponse.Addresses;
                var userAddresses = new Dictionary<string, Address>(addresses.Count);

                if (addresses.Count >= 150)
                {
                    _logger.LogWarning("Number of user addresses retrieved is {NumberOfAddresses}, there might be more", addresses.Count);
                }

                var user = _userClient.GetCachedUser() ?? await _userClient.GetUserAsync(cancellationToken).ConfigureAwait(false);

                var activeUserKeys = user.Keys.Where(x => x.IsActive).ToList();
                if (activeUserKeys.Count == 0)
                {
                    throw new CryptographicException("No active user key was found.");
                }

                foreach (var address in addresses.OrderBy(x => x.Order))
                {
                    int? primaryKeyIndex = null;

                    var addressKeys = new List<AddressKey>(address.Keys.Count);
                    addressKeys.AddRange(
                        address.Keys.Where(k => k.IsActive).Select(
                            (key, index) =>
                            {
                                if (key.IsPrimary)
                                {
                                    primaryKeyIndex = index;
                                }

                                var isLegacyScheme = string.IsNullOrEmpty(key.Token) || string.IsNullOrEmpty(key.Signature);

                                var passphrase = !isLegacyScheme
                                    ? GetPassphrase(key.Id, key.Token!, key.Signature!, activeUserKeys)
                                    : GetLegacyPassphrase(key.Id);

                                var privateKey = PrivatePgpKey.FromArmored(key.PrivateKey, passphrase);

                                return new AddressKey(key.Id, privateKey, (key.Flags & AddressKeyFlags.IsAllowedForEncryption) > 0);
                            }));

                    _cache.Set(
                        new PublicKeysCacheKey(address.EmailAddress),
                        addressKeys.ConvertAll(addressKey => addressKey.PrivateKey.PublicKey).AsReadOnly());

                    if (address.Status != AddressStatus.Enabled)
                    {
                        continue;
                    }

                    if (primaryKeyIndex is null)
                    {
                        _logger.LogError("Address of ID {AddressID} has no primary key", address.Id);
                        continue;
                    }

                    var value = new Address(
                        address.Id,
                        address.EmailAddress,
                        address.Status,
                        addressKeys.AsReadOnly(),
                        primaryKeyIndex.Value);

                    userAddresses.Add(address.Id, value);
                }

                return new ReadOnlyDictionary<string, Address>(userAddresses);
            },
            _cacheSemaphore,
            cancellationToken).ConfigureAwait(false);
    }

    private ReadOnlyMemory<byte> GetPassphrase(string addressKeyId, string token, string signature, IReadOnlyCollection<UserKey> userKeys)
    {
        var userPrivateKeys = userKeys.Select(
            userKey =>
            {
                var userPrivateKeyPassphrase = _keyPassphraseProvider.GetPassphrase(userKey.Id);
                return PrivatePgpKey.FromArmored(userKey.PrivateKey, userPrivateKeyPassphrase);
            }).ToList();

        var addressKeyPassphraseDecrypter =
            _pgpTransformerFactory.CreateVerificationCapableDecrypter(userPrivateKeys, userPrivateKeys.Select(key => key.PublicKey));

        try
        {
            var result = addressKeyPassphraseDecrypter.DecryptAndVerify(token, signature, out var verificationVerdict);

            LogIfSignatureIsInvalid(verificationVerdict, addressKeyId);

            return result;
        }
        catch (Exception ex) when (ex is CryptographicException or KeyPassphraseUnavailableException)
        {
            throw ex.ToDecryptionException("address key", addressKeyId, "passphrase");
        }
    }

    private ReadOnlyMemory<byte> GetLegacyPassphrase(string addressKeyId)
    {
        return _keyPassphraseProvider.GetPassphrase(addressKeyId);
    }

    private void LogIfSignatureIsInvalid(VerificationVerdict verdict, string addressKeyId)
    {
        if (verdict == VerificationVerdict.ValidSignature)
        {
            return;
        }

        // TODO: pass the verification failure as result for marking nodes as suspicious.
        _logger.LogWarning("Signature problem on passphrase of address key with ID {Id}: {Code}", addressKeyId, verdict);
    }

    private record struct PublicKeysCacheKey(string EmailAddress);
}
