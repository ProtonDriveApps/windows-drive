using System.Security;
using Proton.Cryptography.Pgp;
using Proton.Drive.Sdk.Sync.Client.Cryptography.Pgp;

namespace Proton.Drive.Sdk.Sync.Client.Cryptography;

internal interface ICryptographyService
{
    Task<(ISigningCapablePgpMessageProducer Encrypter, Address Address)> CreateMainShareKeyPassphraseEncrypterAsync(CancellationToken cancellationToken);

    Task<(ISigningCapablePgpMessageProducer Encrypter, Address Address)> CreateShareKeyPassphraseEncrypterAsync(
        string mainShareAddressId,
        CancellationToken cancellationToken);

    Task<(ISigningCapablePgpMessageProducer Encrypter, Address SignatureAddress)> CreateNodeNameAndKeyPassphraseEncrypterAsync(
        PgpPublicKey publicKey,
        string signatureAddressId,
        CancellationToken cancellationToken);

    ISigningCapablePgpMessageProducer CreateNodeNameAndKeyPassphraseEncrypter(PgpPublicKey publicKey, PgpPrivateKey signatureKey);

    public ISigningCapablePgpMessageProducer CreateNodeNameAndKeyPassphraseEncrypter(
        PgpPublicKey publicKey,
        PgpSessionKey sessionKey,
        Address signatureAddress);

    Task<(ISigningCapablePgpMessageProducer Encrypter, Address SignatureAddress)> CreateNodeNameAndKeyPassphraseEncrypterAsync(
        PgpPublicKey publicKey,
        PgpSessionKey sessionKey,
        string signatureAddressId,
        CancellationToken cancellationToken);

    ISigningCapablePgpMessageProducer CreateHashKeyEncrypter(PgpPublicKey encryptionKey, PgpPrivateKey signatureKey);

    Task<IVerificationCapablePgpDecrypter> CreateShareKeyPassphraseDecrypterAsync(
        IReadOnlyCollection<string> addressIds,
        string signatureEmailAddress,
        CancellationToken cancellationToken);

    Task<IVerificationCapablePgpDecrypter> CreateNodeNameAndKeyPassphraseDecrypterAsync(
        PgpPrivateKey parentNodeOrShareKey,
        string? signatureEmailAddress,
        CancellationToken cancellationToken);

    IVerificationCapablePgpDecrypter CreateHashKeyDecrypter(PgpPrivateKey privateKey, PgpPublicKey verificationKey);

    Task<IVerificationCapablePgpDecrypter> CreateFileContentsBlockKeyDecrypterAsync(
        PgpPrivateKey nodeKey,
        string? signatureEmailAddress,
        CancellationToken cancellationToken);

    IPgpDecrypter CreateFileContentsBlockDecrypter(PgpPrivateKey nodeKey);

    PgpPrivateKey GenerateShareOrNodeKey();

    ReadOnlyMemory<byte> GeneratePassphrase();

    (ReadOnlyMemory<byte> KeyPacket, PgpSessionKey SessionKey, string SessionKeySignature) GenerateFileContentKeyPacket(
        PgpPublicKey publicKey,
        PgpPrivateKey signatureKey,
        string? fileName = null);

    ReadOnlyMemory<byte> GenerateHashKey();

    string HashNodeNameHex(byte[] hashKey, string nodeName);

    string HashContentDigestHex(byte[] hashKey, string digest);

    ReadOnlyMemory<byte> DeriveSecretFromPassword(SecureString password, ReadOnlySpan<byte> salt);
}
