using System.Buffers.Text;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Proton.Cryptography.Pgp;
using Proton.Cryptography.Srp;
using Proton.Drive.Sdk.Sync.Client.Cryptography.Pgp;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Client.Cryptography;

internal sealed class CryptographyService : ICryptographyService
{
    private const int PassphraseRandomBytesLength = 32;
    private const int HashKeyRandomBytesLength = 32;

    private static readonly int PassphraseMaxUtf8Length = Base64.GetMaxEncodedToUtf8Length(PassphraseRandomBytesLength);
    private static readonly int HashKeyMaxUtf8Length = Base64.GetMaxEncodedToUtf8Length(HashKeyRandomBytesLength);

    private readonly IPgpTransformerFactory _pgpTransformerFactory;
    private readonly Lazy<IAddressKeyProvider> _addressKeyProvider;

    public CryptographyService(
        IPgpTransformerFactory pgpTransformerFactory,
        Func<IAddressKeyProvider> addressKeyProviderFactory)
    {
        _pgpTransformerFactory = pgpTransformerFactory;
        _addressKeyProvider = new Lazy<IAddressKeyProvider>(addressKeyProviderFactory);
    }

    public async Task<(ISigningCapablePgpMessageProducer Encrypter, Address Address)> CreateMainShareKeyPassphraseEncrypterAsync(
        CancellationToken cancellationToken)
    {
        var address = await _addressKeyProvider.Value.GetUserDefaultAddressAsync(cancellationToken).ConfigureAwait(false);
        var primaryAddressKey = address.GetPrimaryKey();

        var encrypter = _pgpTransformerFactory.CreateMessageAndSignatureProducingEncrypter(
            primaryAddressKey.PrivateKey.ToPublic(),
            primaryAddressKey.PrivateKey);
        return (encrypter, address);
    }

    public async Task<(ISigningCapablePgpMessageProducer Encrypter, Address Address)> CreateShareKeyPassphraseEncrypterAsync(
        string mainShareAddressId,
        CancellationToken cancellationToken)
    {
        var address = await _addressKeyProvider.Value.GetAddressAsync(mainShareAddressId, cancellationToken).ConfigureAwait(false);
        var primaryAddressKey = address.GetPrimaryKey();

        var encrypter = _pgpTransformerFactory.CreateMessageAndSignatureProducingEncrypter(
            primaryAddressKey.PrivateKey.ToPublic(),
            primaryAddressKey.PrivateKey);

        return (encrypter, address);
    }

    public async Task<(ISigningCapablePgpMessageProducer Encrypter, Address SignatureAddress)> CreateNodeNameAndKeyPassphraseEncrypterAsync(
        PgpPublicKey publicKey,
        string signatureAddressId,
        CancellationToken cancellationToken)
    {
        var signatureAddress = await _addressKeyProvider.Value.GetAddressAsync(signatureAddressId, cancellationToken).ConfigureAwait(false);

        var encrypter = _pgpTransformerFactory.CreateMessageAndSignatureProducingEncrypter(
            publicKey,
            signatureAddress.GetPrimaryKey().PrivateKey);

        return (encrypter, signatureAddress);
    }

    public ISigningCapablePgpMessageProducer CreateNodeNameAndKeyPassphraseEncrypter(PgpPublicKey publicKey, PgpPrivateKey signatureKey)
    {
        return _pgpTransformerFactory.CreateMessageAndSignatureProducingEncrypter(publicKey, signatureKey);
    }

    public ISigningCapablePgpMessageProducer CreateNodeNameAndKeyPassphraseEncrypter(PgpPublicKey publicKey, PgpSessionKey sessionKey, Address signatureAddress)
    {
        var primaryAddressKey = signatureAddress.GetPrimaryKey();

        var encrypter = _pgpTransformerFactory.CreateMessageAndSignatureProducingEncrypter(
            publicKey,
            sessionKey,
            primaryAddressKey.PrivateKey);

        return encrypter;
    }

    public async Task<(ISigningCapablePgpMessageProducer Encrypter, Address SignatureAddress)> CreateNodeNameAndKeyPassphraseEncrypterAsync(
        PgpPublicKey publicKey,
        PgpSessionKey sessionKey,
        string signatureAddressId,
        CancellationToken cancellationToken)
    {
        var signatureAddress = await _addressKeyProvider.Value.GetAddressAsync(signatureAddressId, cancellationToken).ConfigureAwait(false);

        var encrypter = _pgpTransformerFactory.CreateMessageAndSignatureProducingEncrypter(
            publicKey,
            sessionKey,
            signatureAddress.GetPrimaryKey().PrivateKey);

        return (encrypter, signatureAddress);
    }

    public ISigningCapablePgpMessageProducer CreateHashKeyEncrypter(PgpPublicKey encryptionKey, PgpPrivateKey signatureKey)
    {
        return _pgpTransformerFactory.CreateMessageAndSignatureProducingEncrypter(encryptionKey, signatureKey);
    }

    public async Task<IVerificationCapablePgpDecrypter> CreateShareKeyPassphraseDecrypterAsync(
        IReadOnlyCollection<string> addressIds,
        string signatureEmailAddress,
        CancellationToken cancellationToken)
    {
        var addressKeys = await _addressKeyProvider.Value.GetAddressKeysAsync(addressIds, cancellationToken).ConfigureAwait(false);
        var publicAddressKeys = await _addressKeyProvider.Value.GetPublicKeysForEmailAddressAsync(signatureEmailAddress, cancellationToken)
            .ConfigureAwait(false);

        return _pgpTransformerFactory.CreateVerificationCapableDecrypter(addressKeys.Select(x => x.PrivateKey).ToList(), publicAddressKeys);
    }

    public async Task<IVerificationCapablePgpDecrypter> CreateNodeNameAndKeyPassphraseDecrypterAsync(
        PgpPrivateKey parentNodeOrShareKey,
        string? signatureEmailAddress,
        CancellationToken cancellationToken)
    {
        var verificationKeys = !string.IsNullOrEmpty(signatureEmailAddress)
            ? await _addressKeyProvider.Value.GetPublicKeysForEmailAddressAsync(signatureEmailAddress, cancellationToken).ConfigureAwait(false)
            : [parentNodeOrShareKey.ToPublic()];

        return _pgpTransformerFactory.CreateVerificationCapableDecrypter([parentNodeOrShareKey], verificationKeys);
    }

    public IVerificationCapablePgpDecrypter CreateHashKeyDecrypter(PgpPrivateKey privateKey, PgpPublicKey verificationKey)
    {
        return _pgpTransformerFactory.CreateVerificationCapableDecrypter([privateKey], [verificationKey]);
    }

    public async Task<IVerificationCapablePgpDecrypter> CreateFileContentsBlockKeyDecrypterAsync(
        PgpPrivateKey nodeKey,
        string? signatureEmailAddress,
        CancellationToken cancellationToken)
    {
        var addressKeys = !string.IsNullOrEmpty(signatureEmailAddress)
            ? await _addressKeyProvider.Value.GetPublicKeysForEmailAddressAsync(signatureEmailAddress, cancellationToken).ConfigureAwait(false)
            : [];

        // The signature e-mail address keys are included for signature verification,
        // because we were singing with the address key in the past, but later switched
        // to signing with the node key.
        var verificationKeys = addressKeys.Prepend(nodeKey.ToPublic());

        return _pgpTransformerFactory.CreateVerificationCapableDecrypter([nodeKey], verificationKeys.ToList());
    }

    public IPgpDecrypter CreateFileContentsBlockDecrypter(PgpPrivateKey nodeKey)
    {
        return _pgpTransformerFactory.CreateDecrypter([nodeKey]);
    }

    public PgpPrivateKey GenerateShareOrNodeKey()
    {
        return PgpPrivateKey.Generate("Drive key", "no-reply@proton.me", KeyGenerationAlgorithm.Default);
    }

    public (ReadOnlyMemory<byte> KeyPacket, PgpSessionKey SessionKey, string SessionKeySignature) GenerateFileContentKeyPacket(
        PgpPublicKey publicKey,
        PgpPrivateKey signatureKey,
        string? fileName = null)
    {
        var sessionKey = PgpSessionKey.Generate();
        var keyPacket = publicKey.EncryptSessionKey(sessionKey);
        var sessionKeyToken = sessionKey.Export();
        var sessionKeySignature = signatureKey.Sign(sessionKeyToken);
        var armoredSessionKeySignature = PgpArmorEncoder.Encode(sessionKeySignature, PgpBlockType.Signature);

        return (keyPacket, sessionKey, Encoding.ASCII.GetString(armoredSessionKeySignature));
    }

    public ReadOnlyMemory<byte> GeneratePassphrase()
    {
        var randomBytes = new byte[PassphraseMaxUtf8Length];
        RandomNumberGenerator.Fill(randomBytes.AsSpan(0, PassphraseRandomBytesLength));
        Base64.EncodeToUtf8InPlace(randomBytes, PassphraseRandomBytesLength, out var length);
        return randomBytes.AsMemory(0, length);
    }

    public ReadOnlyMemory<byte> GenerateHashKey()
    {
        var randomBytes = new byte[HashKeyMaxUtf8Length];
        RandomNumberGenerator.Fill(randomBytes.AsSpan(0, HashKeyRandomBytesLength));
        Base64.EncodeToUtf8InPlace(randomBytes, HashKeyRandomBytesLength, out var length);
        return randomBytes.AsMemory(0, length);
    }

    public string HashNodeNameHex(byte[] hashKey, string nodeName)
    {
        // TODO: Check FIPS compliance
        var length = Encoding.UTF8.GetByteCount(nodeName);
        Span<byte> nodeNameSpan = stackalloc byte[length];
        var bytesWritten = Encoding.UTF8.GetBytes(nodeName, nodeNameSpan);

        if (length != bytesWritten)
        {
            throw new CryptographicException($"Cannot compute HMAC name hash: bytes length {length} differs from written bytes {bytesWritten}");
        }

        using var hmac = new HMACSHA256(hashKey);
        Span<byte> hmacSha256Digest = stackalloc byte[HMACSHA256.HashSizeInBytes];

        if (!hmac.TryComputeHash(nodeNameSpan, hmacSha256Digest, out bytesWritten)
            || bytesWritten != HMACSHA256.HashSizeInBytes)
        {
            throw new CryptographicException($"Invalid HMAC name hash: computed hash with length {bytesWritten} instead {HMACSHA256.HashSizeInBytes}");
        }

        return hmacSha256Digest.ToHexString();
    }

    public string HashContentDigestHex(byte[] hashKey, string digest)
    {
        // TODO: Check FIPS compliance
        var length = Encoding.UTF8.GetByteCount(digest);
        Span<byte> digestSpan = stackalloc byte[length];
        var bytesWritten = Encoding.UTF8.GetBytes(digest, digestSpan);

        if (length != bytesWritten)
        {
            throw new CryptographicException($"Cannot compute HMAC content hash: bytes length {length} differs from written bytes {bytesWritten}");
        }

        using var hmacSha256 = new HMACSHA256(hashKey);
        Span<byte> hmacSha256Digest = stackalloc byte[HMACSHA256.HashSizeInBytes];

        if (!hmacSha256.TryComputeHash(digestSpan, hmacSha256Digest, out bytesWritten)
            || bytesWritten != HMACSHA256.HashSizeInBytes)
        {
            throw new CryptographicException($"Invalid HMAC content hash: computed hash with length {bytesWritten} instead {HMACSHA256.HashSizeInBytes}");
        }

        return hmacSha256Digest.ToHexString();
    }

    public ReadOnlyMemory<byte> DeriveSecretFromPassword(SecureString password, ReadOnlySpan<byte> salt)
    {
        Span<byte> utf8Buffer = stackalloc byte[Encoding.UTF8.GetMaxByteCount(password.Length)];
        var unicodePtr = Marshal.SecureStringToGlobalAllocUnicode(password);

        try
        {
            unsafe
            {
                var utf16Span = new ReadOnlySpan<char>(unicodePtr.ToPointer(), password.Length);
                var utf8Length = Encoding.UTF8.GetBytes(utf16Span, utf8Buffer);
                var utf8Password = utf8Buffer[..utf8Length];
                var hash = SrpClient.HashPassword(utf8Password, salt);

                // Skip the first 29 characters which include the algorithm type, the number of rounds and the salt.
                var secret = hash[29..];
                return secret;
            }
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(unicodePtr);
        }
    }
}
