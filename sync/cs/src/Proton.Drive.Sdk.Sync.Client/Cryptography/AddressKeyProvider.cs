using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Proton.Cryptography.Pgp;
using Proton.Drive.Sdk.Sync.Client.Authentication;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Shared.Caching;
using Proton.Drive.Shared.Client;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Logging;

namespace Proton.Drive.Sdk.Sync.Client.Cryptography;

internal sealed class AddressKeyProvider : IAddressKeyProvider
{
    private static readonly object UserAddressesCacheKey = new();

    private readonly IAddressApiClient _addressApiClient;
    private readonly IUserClient _userClient;
    private readonly IKeyApiClient _keyApiClient;
    private readonly IKeyPassphraseProvider _keyPassphraseProvider;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AddressKeyProvider> _logger;

    private readonly SemaphoreSlim _cacheSemaphore = new(1);

    public AddressKeyProvider(
        IAddressApiClient addressApiClient,
        IUserClient userClient,
        IKeyApiClient keyApiClient,
        IKeyPassphraseProvider keyPassphraseProvider,
        IMemoryCache cache,
        ILogger<AddressKeyProvider> logger)
    {
        _addressApiClient = addressApiClient;
        _userClient = userClient;
        _keyApiClient = keyApiClient;
        _keyPassphraseProvider = keyPassphraseProvider;
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

    public async Task<IReadOnlyList<PgpPublicKey>> GetPublicKeysForEmailAddressAsync(string emailAddress, CancellationToken cancellationToken)
    {
        return await _cache.GetOrExclusivelyCreateAsync<IReadOnlyList<PgpPublicKey>>(
            new PublicKeysCacheKey(emailAddress),
            async () =>
            {
                try
                {
                    var publicKeysResponse = await _keyApiClient
                        .GetActivePublicKeysAsync(emailAddress, cancellationToken)
                        .ThrowOnFailure()
                        .ConfigureAwait(false);

                    var publicKeys = new List<PgpPublicKey>(publicKeysResponse.Address.Keys.Count + (publicKeysResponse.UnverifiedAddress?.Keys.Count ?? 0));
                    publicKeys.AddRange(
                        publicKeysResponse.Address.Keys
                            .Union(publicKeysResponse.UnverifiedAddress?.Keys ?? ImmutableList<PublicKeyEntry>.Empty)
                            .Where(keyEntry => keyEntry.Flags.HasFlag(PublicKeyFlags.IsNotCompromised))
                            .Select(entry =>
                            {
                                Span<byte> publicKeyBytes = stackalloc byte[entry.PublicKey.Length];
                                Encoding.ASCII.GetBytes(entry.PublicKey, publicKeyBytes);
                                return PgpPublicKey.Import(publicKeyBytes, PgpEncoding.AsciiArmor);
                            }));

                    return publicKeys.AsReadOnly();
                }
                catch (ApiException ex)
                {
                    _logger.LogWarning("Failed to retrieve public keys for address \"{EmailAddress}\": {ErrorCode}", emailAddress, ex.ResponseCode);

                    if (ex.ResponseCode is ResponseCode.AddressInvalid or ResponseCode.AddressMissing or ResponseCode.AddressDomainExternal or ResponseCode.AddressInvalidKeyTransparency)
                    {
                        return [];
                    }

                    throw;
                }
            },
            _cacheSemaphore,
            cancellationToken).ConfigureAwait(false);
    }

    public void ClearUserAddressesCache()
    {
        _cache.Remove(UserAddressesCacheKey);
        _logger.LogInformation("Cached user addresses invalidated");
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
                    throw new CryptographicException("No active user key was found");
                }

                var userPrivateKeys = activeUserKeys.Select(UnlockUserPrivateKey).ToList();

                foreach (var address in addresses.OrderBy(x => x.Order))
                {
                    var emailAddressToLog = _logger.GetSensitiveValueForLogging(address.EmailAddress);

                    _logger.LogInformation(
                        "User address \"{EmailAddress}\", {Status}, active keys: {NumberOfActiveKeys}, inactive keys: {NumberOfInactiveKeys}, ID: \"{Id}\"",
                        emailAddressToLog,
                        address.Status,
                        address.Keys.Count(k => k.IsActive),
                        address.Keys.Count(k => !k.IsActive),
                        address.Id);

                    int? firstPrimaryKeyIndex = null;

                    var addressKeys = new List<AddressKey>(address.Keys.Count);
                    addressKeys.AddRange(
                        address.Keys.Where(k => k.IsActive).Select(
                            (addressKey, index) =>
                            {
                                if (addressKey.IsPrimary && firstPrimaryKeyIndex is null)
                                {
                                    firstPrimaryKeyIndex = index;

                                    _logger.LogInformation(
                                        "User active and primary address key for \"{EmailAddress}\", ID: \"{KeyId}\", Index: {Index}, Version: {Version}",
                                        emailAddressToLog,
                                        addressKey.Id,
                                        index,
                                        addressKey.Version);
                                }

                                var privateKeyIsUnlocked = TryUnlockAddressPrivateKey(addressKey, userPrivateKeys, out var addressPrivateKey);

                                return new AddressKey(
                                    addressKey.Id,
                                    addressPrivateKey,
                                    IsAllowedForEncryption: (addressKey.Flags & AddressKeyFlags.IsAllowedForEncryption) > 0,
                                    privateKeyIsUnlocked);
                            }));

                    _cache.Set(
                        new PublicKeysCacheKey(address.EmailAddress),
                        addressKeys.ConvertAll(addressKey => addressKey.PrivateKey.ToPublic()).AsReadOnly());

                    if (address.Status != AddressStatus.Enabled)
                    {
                        continue;
                    }

                    if (firstPrimaryKeyIndex is null)
                    {
                        _logger.LogError("User address \"{EmailAddress}\" with ID \"{AddressID}\" has no primary key", emailAddressToLog, address.Id);
                        continue;
                    }

                    var value = new Address(
                        address.Id,
                        address.EmailAddress,
                        address.Status,
                        addressKeys.AsReadOnly(),
                        firstPrimaryKeyIndex.Value);

                    userAddresses.Add(address.Id, value);
                }

                return new ReadOnlyDictionary<string, Address>(userAddresses);
            },
            _cacheSemaphore,
            cancellationToken).ConfigureAwait(false);
    }

    private bool TryUnlockAddressPrivateKey(AddressKeyDto addressKey, List<PgpPrivateKey> userPrivateKeys, out PgpPrivateKey addressPrivateKey)
    {
        Span<byte> privateKeyBytes = stackalloc byte[addressKey.PrivateKey.Length];

        Encoding.ASCII.GetBytes(addressKey.PrivateKey, privateKeyBytes);

        var isLegacyScheme = string.IsNullOrEmpty(addressKey.Token) || string.IsNullOrEmpty(addressKey.Signature);

        var passphrases = !isLegacyScheme
            ? [GetPassphrase(addressKey.Id, addressKey.Token!, addressKey.Signature!, userPrivateKeys)]
            : GetLegacyPassphrases(addressKey.Id);

        foreach (var passphrase in passphrases)
        {
            try
            {
                addressPrivateKey = PgpPrivateKey.ImportAndUnlock(privateKeyBytes, passphrase.Span, PgpEncoding.AsciiArmor);
                return true;
            }
            catch (CryptographicException)
            {
                // Try next passphrase
            }
        }

        _logger.LogWarning("Failed to unlock address private key with ID \"{AddressKeyId}\"", addressKey.Id);

        addressPrivateKey = PgpPrivateKey.Import(privateKeyBytes, PgpEncoding.AsciiArmor);
        return false;
    }

    private ReadOnlyMemory<byte> GetPassphrase(string addressKeyId, string token, string signature, IReadOnlyList<PgpPrivateKey> userPrivateKeys)
    {
        try
        {
            Span<byte> tokenSpan = stackalloc byte[Encoding.UTF8.GetMaxByteCount(token.Length)];
            var tokenByteCount = Encoding.UTF8.GetBytes(token, tokenSpan);
            var signatureBytes = Encoding.UTF8.GetBytes(signature);

            var result = new PgpPrivateKeyRing(userPrivateKeys).DecryptAndVerify(
                tokenSpan[..tokenByteCount],
                signatureBytes,
                new PgpKeyRing(userPrivateKeys),
                out var verificationResult,
                PgpEncoding.AsciiArmor,
                PgpEncoding.AsciiArmor);

            LogIfSignatureIsInvalid(verificationResult.Status, addressKeyId);

            return result;
        }
        catch (Exception ex) when (ex is CryptographicException or KeyPassphraseUnavailableException)
        {
            throw ex.ToDecryptionException("address key", addressKeyId, "passphrase");
        }
    }

    private IReadOnlyCollection<ReadOnlyMemory<byte>> GetLegacyPassphrases(string addressKeyId)
    {
        var passphrases = _keyPassphraseProvider.GetPassphrases();

        return passphrases.TryGetValue(addressKeyId, out var passphrase)
            ? [passphrase]
            : [.. passphrases.Values];
    }

    private PgpPrivateKey UnlockUserPrivateKey(UserKey userKey)
    {
        var passphrases = _keyPassphraseProvider.GetPassphrases();

        Span<byte> privateKeyBytes = stackalloc byte[userKey.PrivateKey.Length];
        Encoding.ASCII.GetBytes(userKey.PrivateKey, privateKeyBytes);

        if (passphrases.TryGetValue(userKey.Id, out var matchingPassphrase))
        {
            try
            {
                return PgpPrivateKey.ImportAndUnlock(privateKeyBytes, matchingPassphrase.Span, PgpEncoding.AsciiArmor);
            }
            catch (CryptographicException)
            {
                // Fallback below: if key/passphrase mapping changed, try other passphrases
            }
        }

        foreach (var (keyId, passphrase) in passphrases)
        {
            if (keyId == userKey.Id)
            {
                continue;
            }

            try
            {
                return PgpPrivateKey.ImportAndUnlock(privateKeyBytes, passphrase.Span, PgpEncoding.AsciiArmor);
            }
            catch (CryptographicException)
            {
                // Try next passphrase
            }
        }

        throw new KeyPassphraseUnavailableException($"No usable passphrase found for user key with ID \"{userKey.Id}\"");
    }

    private void LogIfSignatureIsInvalid(PgpVerificationStatus verdict, string addressKeyId)
    {
        if (verdict == PgpVerificationStatus.Ok)
        {
            return;
        }

        // TODO: pass the verification failure as result for marking nodes as suspicious.
        _logger.LogWarning("Signature problem on passphrase of address key with ID \"{AddressKeyId}\": {Code}", addressKeyId, verdict);
    }

    private record struct PublicKeysCacheKey(string EmailAddress);
}
