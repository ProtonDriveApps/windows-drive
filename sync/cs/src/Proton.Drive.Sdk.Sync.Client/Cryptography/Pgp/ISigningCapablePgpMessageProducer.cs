using Proton.Cryptography.Pgp;

namespace Proton.Drive.Sdk.Sync.Client.Cryptography.Pgp;

internal interface ISigningCapablePgpMessageProducer : IPgpMessageProducer
{
    Stream GetEncryptingAndSigningStream(
        ReadOnlyMemory<byte> plainDataSource,
        PgpEncoding outputEncoding = PgpEncoding.None,
        PgpCompression compression = PgpCompression.None);

    (Stream EncryptingStream, Stream SignatureStream) GetEncryptingAndSignatureStreams(
        ReadOnlyMemory<byte> plainDataSource,
        PgpEncoding signatureEncoding = PgpEncoding.None,
        PgpEncoding outputEncoding = PgpEncoding.None,
        PgpCompression compression = PgpCompression.None);

    (Stream EncryptingStream, Stream SignatureStream, Task<PgpSessionKey> SessionKey) GetEncryptingAndSignatureStreamsWithSessionKey(
        ReadOnlyMemory<byte> plainDataSource,
        PgpEncoding signatureEncoding = PgpEncoding.None,
        PgpEncoding outputEncoding = PgpEncoding.None,
        PgpCompression compression = PgpCompression.None);
}
