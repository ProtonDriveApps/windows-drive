using Proton.Cryptography.Pgp;

namespace Proton.Drive.Sdk.Sync.Client.Cryptography.Pgp;

internal interface IPgpMessageProducer
{
    Stream GetEncryptingStream(
        ReadOnlyMemory<byte> plainDataSource,
        PgpEncoding outputEncoding = PgpEncoding.None,
        PgpCompression compression = PgpCompression.None);

    (Stream Stream, Task<PgpSessionKey> SessionKey) GetEncryptingStreamWithSessionKey(
        ReadOnlyMemory<byte> plainDataSource,
        PgpEncoding outputEncoding = PgpEncoding.None,
        PgpCompression compression = PgpCompression.None);
}
