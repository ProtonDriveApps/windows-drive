using System;
using System.IO;
using System.Text;
using Proton.Security;
using Proton.Security.Cryptography.Abstractions;

namespace ProtonDrive.Client.Cryptography;

public static class PgpEncrypterExtensions
{
    public static string EncryptHashKey(this ISigningCapablePgpMessageProducer encrypter, ReadOnlyMemory<byte> plainData)
    {
        using var plainDataSource = new PlainDataSource(plainData.AsReadOnlyStream());
        using var encryptingStream = encrypter.GetEncryptingAndSigningStream(plainDataSource, PgpArmoring.Ascii);

        using var messageStreamReader = new StreamReader(encryptingStream, Encoding.ASCII);

        var result = messageStreamReader.ReadToEnd();
        return result;
    }

    public static (string Message, string Signature, PgpSessionKey SessionKey) EncryptShareOrNodeKeyPassphrase(
        this ISigningCapablePgpMessageProducer encrypter,
        ReadOnlyMemory<byte> plainData)
    {
        using var plainDataSource = new PlainDataSource(plainData.AsReadOnlyStream());
        var (encryptingStream, signatureStream, sessionKey) = encrypter.GetEncryptingAndSignatureStreamsWithSessionKey(
            plainDataSource,
            DetachedSignatureParameters.ArmoredPlain,
            PgpArmoring.Ascii);

        using var messageStreamReader = new StreamReader(encryptingStream, Encoding.ASCII);
        var message = messageStreamReader.ReadToEnd();

        using var signatureStreamReader = new StreamReader(signatureStream, Encoding.ASCII);
        var signature = signatureStreamReader.ReadToEnd();

        return (message, signature, sessionKey.Result);
    }

    public static string EncryptNodeName(this ISigningCapablePgpMessageProducer encrypter, string plainText)
    {
        var plainData = Encoding.UTF8.GetBytes(plainText);
        using var plainDataSource = new PlainDataSource(new MemoryStream(plainData));
        using var encryptingStream = encrypter.GetEncryptingAndSigningStream(plainDataSource, PgpArmoring.Ascii);

        using var messageStreamReader = new StreamReader(encryptingStream, Encoding.ASCII);

        var result = messageStreamReader.ReadToEnd();
        return result;
    }
}
