using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proton.Security;
using Proton.Security.Cryptography;
using Proton.Security.Cryptography.Abstractions;
using Proton.Security.Cryptography.GopenPgp;
using ProtonDrive.Client.Configuration;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.Client.Cryptography;

internal sealed class CryptographyService : ICryptographyService
{
    private readonly IPgpTransformerFactory _pgpTransformerFactory;
    private readonly Lazy<IAddressKeyProvider> _addressKeyProvider;
    private readonly IPasswordHasher _passwordHasher;
    private readonly Func<DateTimeOffset> _getTimestampFunction;
    private readonly ILogger<CryptographyService> _logger;

    public CryptographyService(
        IPgpTransformerFactory pgpTransformerFactory,
        Func<IAddressKeyProvider> addressKeyProviderFactory,
        IPasswordHasher passwordHasher,
        IServerTimeProvider serverTimeProvider,
        ILogger<CryptographyService> logger)
    {
        _pgpTransformerFactory = pgpTransformerFactory;
        _addressKeyProvider = new Lazy<IAddressKeyProvider>(addressKeyProviderFactory);
        _passwordHasher = passwordHasher;
        _getTimestampFunction = serverTimeProvider.GetServerTime;
        _logger = logger;
    }

    public async Task<(ISigningCapablePgpMessageProducer Encrypter, Address Address)> CreateMainShareKeyPassphraseEncrypterAsync(
        CancellationToken cancellationToken)
    {
        var address = await _addressKeyProvider.Value.GetUserDefaultAddressAsync(cancellationToken).ConfigureAwait(false);
        var primaryAddressKey = address.GetPrimaryKey();

        var encrypter = _pgpTransformerFactory.CreateMessageAndSignatureProducingEncrypter(
            primaryAddressKey.PrivateKey.PublicKey,
            primaryAddressKey.PrivateKey,
            _getTimestampFunction);
        return (encrypter, address);
    }

    public async Task<(ISigningCapablePgpMessageProducer Encrypter, Address Address)> CreateShareKeyPassphraseEncrypterAsync(
        string mainShareAddressId,
        CancellationToken cancellationToken)
    {
        var address = await _addressKeyProvider.Value.GetAddressAsync(mainShareAddressId, cancellationToken).ConfigureAwait(false);
        var primaryAddressKey = address.GetPrimaryKey();

        var encrypter = _pgpTransformerFactory.CreateMessageAndSignatureProducingEncrypter(
            primaryAddressKey.PrivateKey.PublicKey,
            primaryAddressKey.PrivateKey,
            _getTimestampFunction);

        return (encrypter, address);
    }

    public async Task<(ISigningCapablePgpMessageProducer Encrypter, Address SignatureAddress)> CreateNodeNameAndKeyPassphraseEncrypterAsync(
        PublicPgpKey publicKey,
        string signatureAddressId,
        CancellationToken cancellationToken)
    {
        var signatureAddress = await _addressKeyProvider.Value.GetAddressAsync(signatureAddressId, cancellationToken).ConfigureAwait(false);

        var encrypter = _pgpTransformerFactory.CreateMessageAndSignatureProducingEncrypter(
            publicKey,
            signatureAddress.GetPrimaryKey().PrivateKey,
            _getTimestampFunction);

        return (encrypter, signatureAddress);
    }

    public ISigningCapablePgpMessageProducer CreateNodeNameAndKeyPassphraseEncrypter(PublicPgpKey publicKey, PrivatePgpKey signatureKey)
    {
        return _pgpTransformerFactory.CreateMessageAndSignatureProducingEncrypter(publicKey, signatureKey, _getTimestampFunction);
    }

    public ISigningCapablePgpMessageProducer CreateNodeNameAndKeyPassphraseEncrypter(PublicPgpKey publicKey, PgpSessionKey sessionKey, Address signatureAddress)
    {
        var primaryAddressKey = signatureAddress.GetPrimaryKey();

        var encrypter = _pgpTransformerFactory.CreateMessageAndSignatureProducingEncrypter(
            publicKey,
            sessionKey,
            primaryAddressKey.PrivateKey,
            _getTimestampFunction);

        return encrypter;
    }

    public async Task<(ISigningCapablePgpMessageProducer Encrypter, Address SignatureAddress)> CreateNodeNameAndKeyPassphraseEncrypterAsync(
        PublicPgpKey publicKey,
        PgpSessionKey sessionKey,
        string signatureAddressId,
        CancellationToken cancellationToken)
    {
        var signatureAddress = await _addressKeyProvider.Value.GetAddressAsync(signatureAddressId, cancellationToken).ConfigureAwait(false);

        var encrypter = _pgpTransformerFactory.CreateMessageAndSignatureProducingEncrypter(
            publicKey,
            sessionKey,
            signatureAddress.GetPrimaryKey().PrivateKey,
            _getTimestampFunction);

        return (encrypter, signatureAddress);
    }

    public ISigningCapablePgpMessageProducer CreateExtendedAttributesEncrypter(PublicPgpKey publicKey, Address signatureAddress)
    {
        var encrypter = _pgpTransformerFactory.CreateMessageAndSignatureProducingEncrypter(
            publicKey,
            signatureAddress.GetPrimaryKey().PrivateKey,
            _getTimestampFunction);

        return encrypter;
    }

    public ISigningCapablePgpMessageProducer CreateHashKeyEncrypter(PublicPgpKey encryptionKey, PrivatePgpKey signatureKey)
    {
        return _pgpTransformerFactory.CreateMessageAndSignatureProducingEncrypter(encryptionKey, signatureKey, _getTimestampFunction);
    }

    public ISigningCapablePgpDataPacketProducer CreateFileBlockEncrypter(
        PgpSessionKey contentSessionKey,
        PublicPgpKey signaturePublicKey,
        Address signatureAddress)
    {
        var contentEncrypter = new SigningCapablePgpKeyAndDataPacketProducer(
            contentSessionKey,
            signatureAddress.GetPrimaryKey().PrivateKey,
            signaturePublicKey,
            _getTimestampFunction);

        return contentEncrypter;
    }

    public async Task<(ISigningCapablePgpDataPacketProducer Encrypter, Address SignatureAddress)> CreateFileBlockEncrypterAsync(
        PgpSessionKey contentSessionKey,
        PublicPgpKey signaturePublicKey,
        string signatureAddressId,
        CancellationToken cancellationToken)
    {
        var signatureAddress = await _addressKeyProvider.Value.GetAddressAsync(signatureAddressId, cancellationToken).ConfigureAwait(false);

        var contentEncrypter = new SigningCapablePgpKeyAndDataPacketProducer(
            contentSessionKey,
            signatureAddress.GetPrimaryKey().PrivateKey,
            signaturePublicKey,
            _getTimestampFunction);

        return (contentEncrypter, signatureAddress);
    }

    public async Task<IPgpMessageProducer> CreateShareUrlPasswordEncrypterAsync(CancellationToken cancellationToken)
    {
        var address = await _addressKeyProvider.Value.GetUserDefaultAddressAsync(cancellationToken).ConfigureAwait(false);
        var primaryAddressKey = address.GetPrimaryKey();

        return new KeyBasedPgpMessageProducer(primaryAddressKey.PrivateKey.PublicKey, _getTimestampFunction);
    }

    public async Task<IVerificationCapablePgpDecrypter> CreateShareKeyPassphraseDecrypterAsync(
        IReadOnlyCollection<string> addressIds,
        string signatureEmailAddress,
        CancellationToken cancellationToken)
    {
        var addressKeys = await _addressKeyProvider.Value.GetAddressKeysAsync(addressIds, cancellationToken).ConfigureAwait(false);
        return await CreateVerificationCapableDecrypterAsync(addressKeys.Select(x => x.PrivateKey), signatureEmailAddress, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PgpSessionKey> DecryptShareKeyPassphraseSessionKeyAsync(
        string shareId,
        string addressId,
        string signatureEmailAddress,
        string passphraseMessage,
        string passphraseSignature,
        CancellationToken cancellationToken)
    {
        var decrypter = await CreateShareKeyPassphraseDecrypterAsync(new[] { addressId }, signatureEmailAddress, cancellationToken).ConfigureAwait(false);

        var messageSource = new PgpMessageSource(new AsciiStream(passphraseMessage), PgpArmoring.Ascii);
        var signatureSource = new PgpSignatureSource(new AsciiStream(passphraseSignature), PgpArmoring.Ascii);

        await using (messageSource.ConfigureAwait(false))
        await using (signatureSource.ConfigureAwait(false))
        {
            var (stream, verificationTask, sessionKey) = decrypter.GetDecryptingAndVerifyingStreamWithSessionKey(messageSource, signatureSource);
            stream.ReadByte();

            LogIfShareKeyPassphraseIsInvalid(verificationTask, shareId);

            return sessionKey.Result;
        }
    }

    public Task<IVerificationCapablePgpDecrypter> CreateNodeNameAndKeyPassphraseDecrypterAsync(
        PrivatePgpKey parentNodeOrShareKey,
        string? signatureEmailAddress,
        CancellationToken cancellationToken)
    {
        return CreateVerificationCapableDecrypterAsync(new[] { parentNodeOrShareKey }, signatureEmailAddress, cancellationToken);
    }

    public IVerificationCapablePgpDecrypter CreateHashKeyDecrypter(PrivatePgpKey privateKey, PublicPgpKey verificationKey)
    {
        return _pgpTransformerFactory.CreateVerificationCapableDecrypter(new[] { privateKey }, new[] { verificationKey });
    }

    public async Task<IVerificationCapablePgpDecrypter> CreateFileContentsBlockKeyDecrypterAsync(
        PrivatePgpKey nodeKey,
        string signatureEmailAddress,
        CancellationToken cancellationToken)
    {
        var addressKeys = await _addressKeyProvider.Value.GetPublicKeysForEmailAddressAsync(signatureEmailAddress, cancellationToken).ConfigureAwait(false);

        var verificationKeys = addressKeys.Prepend(nodeKey.PublicKey);

        return _pgpTransformerFactory.CreateVerificationCapableDecrypter(new[] { nodeKey }, verificationKeys);
    }

    public Task<IVerificationCapablePgpDecrypter> CreateFileContentsBlockDecrypterAsync(
        PrivatePgpKey nodeKey,
        string signatureEmailAddress,
        CancellationToken cancellationToken)
    {
        return CreateVerificationCapableDecrypterAsync(new[] { nodeKey }, signatureEmailAddress, cancellationToken);
    }

    public async Task<IPgpDecrypter> CreateShareUrlPasswordDecrypterAsync(IReadOnlyCollection<string> emailAddresses, CancellationToken cancellationToken)
    {
        // TODO: Raise exception if list is empty or email addresses are invalid
        var privateKeys = await _addressKeyProvider.Value.GetAddressKeysForEmailAddressesAsync(emailAddresses, cancellationToken).ConfigureAwait(false);
        return _pgpTransformerFactory.CreateDecrypter(privateKeys.Select(x => x.PrivateKey));
    }

    public ReadOnlyMemory<byte> EncryptSessionKey(PgpSessionKey sessionKey, PublicPgpKey publicKey)
    {
        var keyPacketProducer = new PgpKeyAndDataPacketProducer(sessionKey, _getTimestampFunction);
        var newKeyPacket = keyPacketProducer.GetKeyPacket(publicKey);
        return newKeyPacket;
    }

    public ReadOnlyMemory<byte> EncryptSessionKey(PgpSessionKey sessionKey, SecureString password)
    {
        var keyPacketProducer = new PgpKeyAndDataPacketProducer(sessionKey, _getTimestampFunction);
        var newKeyPacket = keyPacketProducer.GetKeyPacket(password);
        return newKeyPacket;
    }

    public PgpSessionKey DecryptSessionKey(ReadOnlyMemory<byte> keyPacket, PrivatePgpKey privateKey)
    {
        var decrypter = new KeyBasedPgpDecrypter(new[] { privateKey });
        var sessionKey = decrypter.DecryptSessionKey(keyPacket);
        return sessionKey;
    }

    public PgpSessionKey DecryptSessionKey(ReadOnlyMemory<byte> keyPacket, SecureString password)
    {
        var decrypter = new PasswordBasedPgpDecrypter(password);
        var sessionKey = decrypter.DecryptSessionKey(keyPacket);
        return sessionKey;
    }

    public async Task<VerificationVerdict> VerifyManifestAsync(
        ReadOnlyMemory<byte> manifest,
        string manifestSignature,
        string signatureEmailAddress,
        CancellationToken cancellationToken)
    {
        var publicKeysForVerification = !string.IsNullOrEmpty(signatureEmailAddress)
            ? await _addressKeyProvider.Value.GetPublicKeysForEmailAddressAsync(signatureEmailAddress, cancellationToken).ConfigureAwait(false)
            : Array.Empty<PublicPgpKey>();

        var verifier = _pgpTransformerFactory.CreateVerificationCapableDecrypter(Array.Empty<PrivatePgpKey>(), publicKeysForVerification);

        var verificationVerdict = await verifier.VerifyAsync(
            manifest,
            new PgpSignatureSource(new AsciiStream(manifestSignature), PgpArmoring.Ascii),
            cancellationToken).ConfigureAwait(false);

        return verificationVerdict;
    }

    public bool PrivateKeyIsValid(PrivatePgpKey privateKey) => privateKey.IsValid();

    public PrivatePgpKey GenerateShareOrNodeKey(ReadOnlyMemory<byte> passphrase)
    {
        return PgpGenerator.GeneratePrivateKey("Drive key", "no-reply@proton.me", passphrase, KeySpecification.X25519(), _getTimestampFunction.Invoke());
    }

    public (ReadOnlyMemory<byte> KeyPacket, PgpSessionKey SessionKey, string SessionKeySignature) GenerateFileContentKeyPacket(
        PublicPgpKey publicKey,
        PrivatePgpKey signatureKey,
        string? fileName = null)
    {
        var (keyPacket, sessionKey) = PgpGenerator.GenerateKeyPacket(publicKey);

        var encrypter = new SigningCapablePgpMessageProducer(publicKey, signatureKey, _getTimestampFunction);
        var sessionKeySource = new PlainDataSource(sessionKey.Data.AsReadOnlyStream());
        using var sessionKeySignatureStream = encrypter.GetSignatureStream(sessionKeySource, DetachedSignatureParameters.ArmoredPlain);

        using var sessionKeySignatureReader = new StreamReader(sessionKeySignatureStream, Encoding.ASCII);
        var sessionKeySignature = sessionKeySignatureReader.ReadToEnd();

        return (keyPacket, sessionKey, sessionKeySignature);
    }

    public ReadOnlyMemory<byte> GeneratePassphrase()
    {
        return GetRandomBase64Bytes(32);
    }

    public ReadOnlyMemory<byte> GenerateSrpSalt()
    {
        return PgpGenerator.GenerateRandomToken(10);
    }

    public ReadOnlyMemory<byte> GenerateEncryptionPasswordSalt()
    {
        return PgpGenerator.GenerateRandomToken(16);
    }

    public ReadOnlyMemory<byte> GenerateHashKey()
    {
        return GetRandomBase64Bytes(32);
    }

    public byte[] HashNodeName(byte[] hashKey, string nodeName)
    {
        // TODO: Check FIPS compliance
        using var hmac = new HMACSHA256(hashKey);
        var digest = hmac.ComputeHash(Encoding.UTF8.GetBytes(nodeName));
        return digest;
    }

    public byte[] HashBlockContent(Stream blockContentStream)
    {
        // TODO: Check FIPS compliance
        using var sha256 = SHA256.Create();
        var digest = sha256.ComputeHash(blockContentStream);
        return digest;
    }

    public ReadOnlyMemory<byte> DeriveSecretFromPassword(SecureString password, ReadOnlySpan<byte> salt)
    {
        var hash = _passwordHasher.Hash(password, salt);

        // Skip the first 29 characters which include the algorithm type, the number of rounds and the salt.
        var secret = hash[29..];

        return secret;
    }

    /// <remarks>
    /// The base64 part is to be compatible with the Web client quirks.
    /// </remarks>
    /// <param name="length">Length of the byte array.</param>
    private static ReadOnlyMemory<byte> GetRandomBase64Bytes(int length)
    {
        var randomBytes = PgpGenerator.GenerateRandomToken(length);
        var base64String = Convert.ToBase64String(randomBytes);
        return Encoding.ASCII.GetBytes(base64String);
    }

    private async Task<IVerificationCapablePgpDecrypter> CreateVerificationCapableDecrypterAsync(
        IReadOnlyCollection<PrivatePgpKey> decryptionKeyRing,
        string? signatureEmailAddress,
        CancellationToken cancellationToken)
    {
        var publicKeysForVerification = !string.IsNullOrEmpty(signatureEmailAddress)
            ? await _addressKeyProvider.Value.GetPublicKeysForEmailAddressAsync(signatureEmailAddress, cancellationToken).ConfigureAwait(false)
            : Array.Empty<PublicPgpKey>();
        return _pgpTransformerFactory.CreateVerificationCapableDecrypter(decryptionKeyRing, publicKeysForVerification);
    }

    private void LogIfShareKeyPassphraseIsInvalid(Task<VerificationVerdict> task, string shareId)
    {
        // TODO: Instead of Task<>, use a type that expresses the guarantee of a result
        Trace.Assert(task.IsCompleted, "Signature verification task is not completed");

        var code = task.Result;
        if (code == VerificationVerdict.ValidSignature)
        {
            return;
        }

        // TODO: pass the verification failure as result for marking shares as suspicious.
        _logger.LogWarning("Signature problem on passphrase of key of share with ID {Id}: {Code}", shareId, code);
    }
}
