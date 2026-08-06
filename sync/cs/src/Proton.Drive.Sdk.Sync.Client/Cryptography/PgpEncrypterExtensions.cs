using System.Text;
using Proton.Cryptography.Pgp;
using Proton.Drive.Sdk.Sync.Client.Cryptography.Pgp;

namespace Proton.Drive.Sdk.Sync.Client.Cryptography;

internal static class PgpEncrypterExtensions
{
    public static string EncryptHashKey(this ISigningCapablePgpMessageProducer encrypter, ReadOnlyMemory<byte> plainData)
    {
        using var encryptingStream = encrypter.GetEncryptingAndSigningStream(plainData, PgpEncoding.AsciiArmor);

        using var messageStreamReader = new StreamReader(encryptingStream, Encoding.ASCII);

        var result = messageStreamReader.ReadToEnd();
        return result;
    }

    public static (string Message, string Signature, PgpSessionKey SessionKey) EncryptShareOrNodeKeyPassphrase(
        this ISigningCapablePgpMessageProducer encrypter,
        ReadOnlyMemory<byte> plainData)
    {
        var (encryptingStream, signatureStream, sessionKey) = encrypter.GetEncryptingAndSignatureStreamsWithSessionKey(
            plainData,
            signatureEncoding: PgpEncoding.AsciiArmor,
            outputEncoding: PgpEncoding.AsciiArmor);

        using var messageStreamReader = new StreamReader(encryptingStream, Encoding.ASCII);
        var message = messageStreamReader.ReadToEnd();

        encryptingStream.Dispose();
        signatureStream.Seek(0, SeekOrigin.Begin);
        using var signatureStreamReader = new StreamReader(signatureStream, Encoding.ASCII);
        var signature = signatureStreamReader.ReadToEnd();

        return (message, signature, sessionKey.Result);
    }

    public static string EncryptNodeName(this ISigningCapablePgpMessageProducer encrypter, string plainText)
    {
        var plainData = Encoding.UTF8.GetBytes(plainText);
        using var encryptingStream = encrypter.GetEncryptingAndSigningStream(plainData, PgpEncoding.AsciiArmor);

        using var messageStreamReader = new StreamReader(encryptingStream, Encoding.ASCII);

        var result = messageStreamReader.ReadToEnd();
        return result;
    }
}
