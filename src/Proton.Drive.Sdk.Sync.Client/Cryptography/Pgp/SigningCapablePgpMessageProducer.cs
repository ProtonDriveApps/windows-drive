using CommunityToolkit.HighPerformance;
using Proton.Cryptography.Pgp;

namespace Proton.Drive.Sdk.Sync.Client.Cryptography.Pgp;

public sealed class SigningCapablePgpMessageProducer : ISigningCapablePgpMessageProducer
{
    private readonly PgpPrivateKeyRing _signingKeyRing;
    private readonly PgpPublicKey _publicKey;
    private readonly PgpSessionKey? _sessionKey;

    public SigningCapablePgpMessageProducer(PgpPublicKey publicKey, PgpPrivateKey signaturePrivateKey)
    {
        _signingKeyRing = signaturePrivateKey;
        _publicKey = publicKey;
    }

    public SigningCapablePgpMessageProducer(PgpPublicKey publicKey, PgpSessionKey sessionKey, PgpPrivateKey signaturePrivateKey)
        : this(publicKey, signaturePrivateKey)
    {
        _sessionKey = sessionKey;
    }

    public Stream GetEncryptingStream(
        ReadOnlyMemory<byte> plainDataSource,
        PgpEncoding outputEncoding = PgpEncoding.None,
        PgpCompression compression = PgpCompression.None)
    {
        var encryptionSecrets = _sessionKey is not null ? new EncryptionSecrets(_publicKey, _sessionKey.Value) : new EncryptionSecrets(_publicKey);
        return PgpEncryptingReadStream.Open(plainDataSource.AsStream(), encryptionSecrets, outputEncoding, compression);
    }

    public (Stream Stream, Task<PgpSessionKey> SessionKey) GetEncryptingStreamWithSessionKey(
        ReadOnlyMemory<byte> plainDataSource,
        PgpEncoding outputEncoding = PgpEncoding.None,
        PgpCompression compression = PgpCompression.None)
    {
        var sessionKey = _sessionKey ?? PgpSessionKey.Generate();
        var stream = PgpEncryptingReadStream.Open(plainDataSource.AsStream(), new EncryptionSecrets(_publicKey, sessionKey), outputEncoding, compression);
        return (stream, Task.FromResult(sessionKey));
    }

    public Stream GetEncryptingAndSigningStream(
        ReadOnlyMemory<byte> plainDataSource,
        PgpEncoding outputEncoding = PgpEncoding.None,
        PgpCompression compression = PgpCompression.None)
    {
        var encryptionSecrets = _sessionKey is not null ? new EncryptionSecrets(_publicKey, _sessionKey.Value) : new EncryptionSecrets(_publicKey);
        return PgpEncryptingReadStream.Open(plainDataSource.AsStream(), encryptionSecrets, _signingKeyRing, outputEncoding, compression);
    }

    public (Stream EncryptingStream, Stream SignatureStream) GetEncryptingAndSignatureStreams(
        ReadOnlyMemory<byte> plainDataSource,
        PgpEncoding signatureEncoding = PgpEncoding.None,
        PgpEncoding outputEncoding = PgpEncoding.None,
        PgpCompression compression = PgpCompression.None)
    {
        var encryptionSecrets = _sessionKey is not null ? new EncryptionSecrets(_publicKey, _sessionKey.Value) : new EncryptionSecrets(_publicKey);

        var signatureOutputStream = new MemoryStream();

        var encryptingStream = PgpEncryptingReadStream.Open(
            plainDataSource.AsStream(),
            signatureOutputStream,
            encryptionSecrets,
            _signingKeyRing,
            outputEncoding,
            compression);

        return (encryptingStream, signatureOutputStream);
    }

    public (Stream EncryptingStream, Stream SignatureStream, Task<PgpSessionKey> SessionKey) GetEncryptingAndSignatureStreamsWithSessionKey(
        ReadOnlyMemory<byte> plainDataSource,
        PgpEncoding signatureEncoding = PgpEncoding.None,
        PgpEncoding outputEncoding = PgpEncoding.None,
        PgpCompression compression = PgpCompression.None)
    {
        var sessionKey = _sessionKey ?? PgpSessionKey.Generate();

        var signatureOutputStream = new MemoryStream();

        var encryptionSecrets = new EncryptionSecrets(_publicKey, sessionKey);
        var encryptingStream = PgpEncryptingReadStream.Open(
            plainDataSource.AsStream(),
            signatureOutputStream,
            encryptionSecrets,
            _signingKeyRing,
            outputEncoding,
            compression);

        return (encryptingStream, signatureOutputStream, Task.FromResult(sessionKey));
    }
}
