using CommunityToolkit.HighPerformance;
using Proton.Cryptography.Pgp;
using Proton.Drive.Sdk.Sync.Client.Cryptography.Pgp;

namespace Proton.Drive.Sdk.Sync.Client.Cryptography;

internal class KeyBasedPgpDecrypter(IReadOnlyList<PgpPrivateKey> privateKeyRing) : IPgpDecrypter
{
    protected PgpPrivateKeyRing PgpPrivateKeyRing { get; } = new(privateKeyRing);

    public PgpSessionKey DecryptSessionKey(ReadOnlyMemory<byte> keyPacket)
    {
        return PgpPrivateKeyRing.DecryptSessionKey(keyPacket.Span);
    }

    public Stream GetDecryptingStream(Stream messageSource)
    {
        return PgpPrivateKeyRing.OpenDecryptingStream(messageSource);
    }

    public (Stream Stream, PgpSessionKey SessionKey) GetDecryptingStreamWithSessionKey(ReadOnlyMemory<byte> armoredMessage)
    {
        var message = PgpArmorDecoder.Decode(armoredMessage.Span);
        var sessionKey = PgpPrivateKeyRing.DecryptSessionKey(message);
        var stream = PgpEncryptingReadStream.Open(armoredMessage.AsStream(), sessionKey, PgpEncoding.AsciiArmor);
        return (stream, sessionKey);
    }
}
