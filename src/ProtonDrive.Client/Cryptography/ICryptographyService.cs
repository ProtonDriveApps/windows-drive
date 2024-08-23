using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Proton.Security.Cryptography;
using Proton.Security.Cryptography.Abstractions;

namespace ProtonDrive.Client.Cryptography;

public interface ICryptographyService
{
    Task<(ISigningCapablePgpMessageProducer Encrypter, Address Address)> CreateMainShareKeyPassphraseEncrypterAsync(CancellationToken cancellationToken);

    Task<(ISigningCapablePgpMessageProducer Encrypter, Address Address)> CreateShareKeyPassphraseEncrypterAsync(
        string mainShareAddressId,
        CancellationToken cancellationToken);

    Task<(ISigningCapablePgpMessageProducer Encrypter, Address SignatureAddress)> CreateNodeNameAndKeyPassphraseEncrypterAsync(
        PublicPgpKey publicKey,
        string signatureAddressId,
        CancellationToken cancellationToken);

    ISigningCapablePgpMessageProducer CreateNodeNameAndKeyPassphraseEncrypter(PublicPgpKey publicKey, PrivatePgpKey signatureKey);

    public ISigningCapablePgpMessageProducer CreateNodeNameAndKeyPassphraseEncrypter(
        PublicPgpKey publicKey,
        PgpSessionKey sessionKey,
        Address signatureAddress);

    Task<(ISigningCapablePgpMessageProducer Encrypter, Address SignatureAddress)> CreateNodeNameAndKeyPassphraseEncrypterAsync(
        PublicPgpKey publicKey,
        PgpSessionKey sessionKey,
        string signatureAddressId,
        CancellationToken cancellationToken);

    ISigningCapablePgpMessageProducer CreateExtendedAttributesEncrypter(PublicPgpKey publicKey, Address signatureAddress);

    ISigningCapablePgpMessageProducer CreateHashKeyEncrypter(PublicPgpKey encryptionKey, PrivatePgpKey signatureKey);

    ISigningCapablePgpDataPacketProducer CreateFileBlockEncrypter(
        PgpSessionKey contentSessionKey,
        PublicPgpKey signaturePublicKey,
        Address signatureAddress);

    public Task<(ISigningCapablePgpDataPacketProducer Encrypter, Address SignatureAddress)> CreateFileBlockEncrypterAsync(
        PgpSessionKey contentSessionKey,
        PublicPgpKey signaturePublicKey,
        string signatureAddressId,
        CancellationToken cancellationToken);

    Task<IVerificationCapablePgpDecrypter> CreateShareKeyPassphraseDecrypterAsync(
        IReadOnlyCollection<string> addressIds,
        string signatureEmailAddress,
        CancellationToken cancellationToken);

    Task<PgpSessionKey> DecryptShareKeyPassphraseSessionKeyAsync(
        string shareId,
        string addressId,
        string signatureEmailAddress,
        string passphraseMessage,
        string passphraseSignature,
        CancellationToken cancellationToken);

    Task<IVerificationCapablePgpDecrypter> CreateNodeNameAndKeyPassphraseDecrypterAsync(
        PrivatePgpKey parentNodeOrShareKey,
        string? signatureEmailAddress,
        CancellationToken cancellationToken);

    IVerificationCapablePgpDecrypter CreateHashKeyDecrypter(PrivatePgpKey privateKey, PublicPgpKey verificationKey);

    Task<IVerificationCapablePgpDecrypter> CreateFileContentsBlockKeyDecrypterAsync(
        PrivatePgpKey nodeKey,
        string signatureEmailAddress,
        CancellationToken cancellationToken);

    Task<IVerificationCapablePgpDecrypter> CreateFileContentsBlockDecrypterAsync(
        PrivatePgpKey nodeKey,
        string signatureEmailAddress,
        CancellationToken cancellationToken);

    Task<IPgpMessageProducer> CreateShareUrlPasswordEncrypterAsync(CancellationToken cancellationToken);

    Task<IPgpDecrypter> CreateShareUrlPasswordDecrypterAsync(IReadOnlyCollection<string> emailAddresses, CancellationToken cancellationToken);

    /// <summary>
    /// Encrypts a session key into a key packet using an asymmetric key.
    /// </summary>
    /// <returns>Return the key packet containing the encrypted session key.</returns>
    ReadOnlyMemory<byte> EncryptSessionKey(PgpSessionKey sessionKey, PublicPgpKey publicKey);

    /// <summary>
    /// Encrypts a session key into a key packet using a password.
    /// </summary>
    /// <returns>Return the key packet containing the encrypted session key.</returns>
    ReadOnlyMemory<byte> EncryptSessionKey(PgpSessionKey sessionKey, SecureString password);

    /// <summary>
    /// Decrypts a session key from a key packet using an asymmetric key.
    /// </summary>
    /// <returns>Returns the decrypted session key.</returns>
    PgpSessionKey DecryptSessionKey(ReadOnlyMemory<byte> keyPacket, PrivatePgpKey privateKey);

    /// <summary>
    /// Decrypts a session key from a key packet using a password.
    /// </summary>
    /// <returns>Returns the decrypted session key.</returns>
    PgpSessionKey DecryptSessionKey(ReadOnlyMemory<byte> keyPacket, SecureString password);

    Task<VerificationVerdict> VerifyManifestAsync(
        ReadOnlyMemory<byte> manifest,
        string manifestSignature,
        string signatureEmailAddress,
        CancellationToken cancellationToken);

    bool PrivateKeyIsValid(PrivatePgpKey privateKey);

    PrivatePgpKey GenerateShareOrNodeKey(ReadOnlyMemory<byte> passphrase);

    ReadOnlyMemory<byte> GeneratePassphrase();

    ReadOnlyMemory<byte> GenerateSrpSalt();

    ReadOnlyMemory<byte> GenerateEncryptionPasswordSalt();

    (ReadOnlyMemory<byte> KeyPacket, PgpSessionKey SessionKey, string SessionKeySignature) GenerateFileContentKeyPacket(
        PublicPgpKey publicKey,
        PrivatePgpKey signatureKey,
        string? fileName = null);

    ReadOnlyMemory<byte> GenerateHashKey();

    byte[] HashNodeName(byte[] key, string nodeName);

    byte[] HashBlockContent(Stream blockContentStream);

    ReadOnlyMemory<byte> DeriveSecretFromPassword(SecureString password, ReadOnlySpan<byte> salt);
}
