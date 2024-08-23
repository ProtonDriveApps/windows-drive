using System;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using ProtonDrive.Client.Cryptography;
using ProtonDrive.Shared.Repository;
using ProtonDrive.Shared.Security.Cryptography;

namespace ProtonDrive.Client.Repository;

internal class ProtectedKeyPassphraseRepository : IProtectedRepository<KeyPassphrases>
{
    private readonly IDataProtectionProvider _protectionProvider;
    private readonly IRepository<KeyPassphrases> _origin;
    private readonly ILogger<ProtectedKeyPassphraseRepository> _logger;

    public ProtectedKeyPassphraseRepository(
        IDataProtectionProvider protectionProvider,
        IRepository<KeyPassphrases> origin,
        ILogger<ProtectedKeyPassphraseRepository> logger)
    {
        _protectionProvider = protectionProvider;
        _origin = origin;
        _logger = logger;
    }

    public KeyPassphrases? Get()
    {
        return ToUnprotected(_origin.Get());
    }

    public void Set(KeyPassphrases? value)
    {
        _origin.Set(ToProtected(value));
    }

    private KeyPassphrases? ToProtected(KeyPassphrases? data)
    {
        if (data == null)
        {
            return data;
        }

        try
        {
            return new KeyPassphrases(
                data.Passphrases.ToImmutableDictionary(i => i.Key, i => ToProtected(i.Value)));
        }
        catch (CryptographicException)
        {
            _logger.LogError("Failed to protect KeyPassphrases");

            return null;
        }
    }

    private KeyPassphrases? ToUnprotected(KeyPassphrases? data)
    {
        if (data == null)
        {
            return data;
        }

        try
        {
            return new KeyPassphrases(
                data.Passphrases.ToImmutableDictionary(i => i.Key, i => ToUnprotected(i.Value)));
        }
        catch (CryptographicException)
        {
            _logger.LogError("Failed to unprotect KeyPassphrases");

            return null;
        }
    }

    private ReadOnlyMemory<byte> ToProtected(ReadOnlyMemory<byte> value)
    {
        return _protectionProvider.Protect(value);
    }

    private ReadOnlyMemory<byte> ToUnprotected(ReadOnlyMemory<byte> value)
    {
        return _protectionProvider.Unprotect(value);
    }
}
