using Proton.Cryptography.Pgp;

namespace Proton.Drive.Sdk.Sync.Client.Cryptography.Pgp;

internal interface IPgpDecrypter
{
    PgpSessionKey DecryptSessionKey(ReadOnlyMemory<byte> keyPacket);

    Stream GetDecryptingStream(Stream messageSource);

    /// <summary>
    /// Gets a stream from which the plain data can be read, as well as the decrypted session key.
    /// </summary>
    /// <returns>
    /// A value tuple that contains the decrypting stream,
    /// and a task for the decrypted session key for which the result will be set at the first read operation on the stream.
    /// </returns>
    (Stream Stream, PgpSessionKey SessionKey) GetDecryptingStreamWithSessionKey(ReadOnlyMemory<byte> armoredMessage);
}
