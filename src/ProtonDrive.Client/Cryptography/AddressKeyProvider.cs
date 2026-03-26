using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Proton.Cryptography.Pgp;
using ProtonDrive.Client.Authentication;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Shared.Caching;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Logging;

namespace ProtonDrive.Client.Cryptography;

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
                    var emailAddressToLog = _logger.GetSensitiveValueForLogging(address.EmailAddress);

                    _logger.LogInformation(
                        "User address \"{EmailAddress}\", {Status}, active keys: {NumberOfActiveKeys}, inactive keys: {NumberOfInactiveKeys}, ID={Id}",
                        emailAddressToLog,
                        address.Status,
                        address.Keys.Count(k => k.IsActive),
                        address.Keys.Count(k => !k.IsActive),
                        address.Id);

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

                                Span<byte> privateKeyBytes = stackalloc byte[key.PrivateKey.Length];

                                Encoding.ASCII.GetBytes(key.PrivateKey, privateKeyBytes);

                                PgpPrivateKey privateKey;
                                bool privateKeyIsUnlocked;

                                try
                                {
                                    privateKey = PgpPrivateKey.ImportAndUnlock(privateKeyBytes, passphrase.Span, PgpEncoding.AsciiArmor);
                                    privateKeyIsUnlocked = true;
                                }
                                catch
                                {
                                    privateKey = PgpPrivateKey.Import(privateKeyBytes, PgpEncoding.AsciiArmor);
                                    privateKeyIsUnlocked = false;
                                }

                                return new AddressKey(key.Id, privateKey, (key.Flags & AddressKeyFlags.IsAllowedForEncryption) > 0, privateKeyIsUnlocked);
                            }));

                    _cache.Set(
                        new PublicKeysCacheKey(address.EmailAddress),
                        addressKeys.ConvertAll(addressKey => addressKey.PrivateKey.ToPublic()).AsReadOnly());

                    if (address.Status != AddressStatus.Enabled)
                    {
                        continue;
                    }

                    if (primaryKeyIndex is null)
                    {
                        _logger.LogError("User address \"{EmailAddress}\" with ID {AddressID} has no primary key", emailAddressToLog, address.Id);
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
        var userPrivateKeys = userKeys.Select(userKey =>
        {
            var userPrivateKeyPassphrase = _keyPassphraseProvider.GetPassphrase(userKey.Id);
            Span<byte> privateKeyBytes = stackalloc byte[userKey.PrivateKey.Length];
            Encoding.ASCII.GetBytes(userKey.PrivateKey, privateKeyBytes);
            return PgpPrivateKey.ImportAndUnlock(privateKeyBytes, userPrivateKeyPassphrase.Span, PgpEncoding.AsciiArmor);
        }).ToList();

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

    private ReadOnlyMemory<byte> GetLegacyPassphrase(string addressKeyId)
    {
        return _keyPassphraseProvider.GetPassphrase(addressKeyId);
    }

    private void LogIfSignatureIsInvalid(PgpVerificationStatus verdict, string addressKeyId)
    {
        if (verdict == PgpVerificationStatus.Ok)
        {
            return;
        }

        // TODO: pass the verification failure as result for marking nodes as suspicious.
        _logger.LogWarning("Signature problem on passphrase of address key with ID {Id}: {Code}", addressKeyId, verdict);
    }

    private record struct PublicKeysCacheKey(string EmailAddress);
}
