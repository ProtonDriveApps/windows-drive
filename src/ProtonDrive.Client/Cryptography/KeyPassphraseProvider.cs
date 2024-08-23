using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Repository;

namespace ProtonDrive.Client.Cryptography;

internal class KeyPassphraseProvider : IKeyPassphraseProvider
{
    private readonly IKeyApiClient _keyApiClient;
    private readonly ICryptographyService _cryptographyService;
    private readonly IProtectedRepository<KeyPassphrases> _repository;
    private readonly ILogger<KeyPassphraseProvider> _logger;

    public KeyPassphraseProvider(
        IKeyApiClient keyApiClient,
        ICryptographyService cryptographyService,
        IProtectedRepository<KeyPassphrases> repository,
        ILogger<KeyPassphraseProvider> logger)
    {
        _keyApiClient = keyApiClient;
        _cryptographyService = cryptographyService;
        _repository = repository;
        _logger = logger;
    }

    public bool ContainsAtLeastOnePassphrase => Passphrases.Count > 0;

    private IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Passphrases
    {
        get => _repository.Get()?.Passphrases ?? ImmutableDictionary<string, ReadOnlyMemory<byte>>.Empty;
        set
        {
            _repository.Set(new KeyPassphrases(value));

            if (!value.Any())
            {
                _logger.LogDebug("Key passphrases cleared");
            }
            else
            {
                foreach (var key in value.Keys)
                {
                    _logger.LogDebug("Key passphrase added for key ID={KeyId}", key);
                }
            }
        }
    }

    public async Task CalculatePassphrasesAsync(SecureString password, CancellationToken cancellationToken)
    {
        var keySaltListResponse = await _keyApiClient.GetKeySaltsAsync(cancellationToken).ThrowOnFailure().ConfigureAwait(false);

        Passphrases = keySaltListResponse.KeySalts
            .Where(x => x.Value is not null)
            .ToImmutableDictionary(
                keySalt => keySalt.KeyId,
                keySalt => _cryptographyService.DeriveSecretFromPassword(password, Convert.FromBase64String(keySalt.Value!)));
    }

    public void ClearPassphrases()
    {
        Passphrases = ImmutableDictionary<string, ReadOnlyMemory<byte>>.Empty;
    }

    public ReadOnlyMemory<byte> GetPassphrase(string keyId)
    {
        if (!Passphrases.TryGetValue(keyId, out var passphrase))
        {
            throw new KeyPassphraseUnavailableException($"No salt found for key ID={keyId}");
        }

        return passphrase;
    }
}
