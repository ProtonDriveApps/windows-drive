using System.Text;
using Proton.Cryptography.Pgp;

namespace Proton.Drive.Sdk.Sync.Client.Cryptography.Pgp;

internal static class PgpDecrypterExtensions
{
    public static ReadOnlyMemory<byte> DecryptAndVerify(
        this IVerificationCapablePgpDecrypter decrypter,
        string armoredSignedMessage,
        out PgpVerificationStatus verificationStatus)
    {
        var result = Decrypt(
            armoredSignedMessage,
            decrypter.GetDecryptingAndVerifyingStream,
            result => result.DecryptingStream,
            (plainData, result) => (
                PlainData: plainData,
                StreamProvisionResult: result.DecryptingStream,
                VerificationStatus: result.GetVerificationStatus.Invoke()));

        verificationStatus = result.VerificationStatus;
        return result.PlainData;
    }

    public static ReadOnlyMemory<byte> DecryptAndVerify(
        this IVerificationCapablePgpDecrypter decrypter,
        string armoredMessage,
        string armoredSignature,
        out PgpVerificationStatus verificationStatus,
        out PgpSessionKey sessionKey)
    {
        var result = Decrypt(
            armoredMessage,
            message => decrypter.GetDecryptingAndVerifyingStreamWithSessionKey(message, Encoding.ASCII.GetBytes(armoredSignature)),
            streamProvisionResult => streamProvisionResult.DecryptionStream,
            (plainData, streamProvisionResult) => (
                PlainData: plainData,
                StreamProvisionResult: streamProvisionResult,
                VerificationStatus: streamProvisionResult.GetVerificationStatus.Invoke()));

        verificationStatus = result.VerificationStatus;
        sessionKey = result.StreamProvisionResult.SessionKey;
        return result.PlainData;
    }

    public static string DecryptAndVerifyText(
        this IVerificationCapablePgpDecrypter decrypter,
        string armoredSignedMessage,
        out PgpVerificationStatus verificationStatus,
        out PgpSessionKey sessionKey)
    {
        var armoredSignedMessageBytes = Encoding.ASCII.GetBytes(armoredSignedMessage);
        var streamResult = decrypter.GetDecryptingAndVerifyingStreamWithSessionKey(armoredSignedMessageBytes);
        var result = DecryptText(
            streamResult,
            streamProvisionResult => streamProvisionResult.DecryptionStream,
            (plainData, result) => (
                PlainText: plainData,
                StreamProvisionResult: result,
                VerificationStatus: result.GetVerificationStatus.Invoke()));

        verificationStatus = result.VerificationStatus;
        sessionKey = result.StreamProvisionResult.SessionKey;
        return result.PlainText;
    }

    private static TResult Decrypt<TStreamProvisionResult, TResult>(
        string armoredMessage,
        Func<ReadOnlyMemory<byte>, TStreamProvisionResult> getStreamFunction,
        Func<TStreamProvisionResult, Stream> streamGetter,
        Func<ReadOnlyMemory<byte>, TStreamProvisionResult, TResult> createResultFunction)
    {
        var buffer = new byte[armoredMessage.Length];

        using var outputStream = new MemoryStream(buffer, true);
        var getStreamResult = getStreamFunction.Invoke(Encoding.ASCII.GetBytes(armoredMessage));
        using var decryptingStream = streamGetter.Invoke(getStreamResult);
        int byteCount;
        var totalByteCount = 0;
        do
        {
            byteCount = decryptingStream.Read(buffer, totalByteCount, buffer.Length - totalByteCount);
            totalByteCount += byteCount;
        }
        while (byteCount > 0);

        return createResultFunction.Invoke(buffer.AsMemory(0, totalByteCount), getStreamResult);
    }

    private static TResult DecryptText<TStreamResult, TResult>(
        TStreamResult streamResult,
        Func<TStreamResult, Stream> streamGetter,
        Func<string, TStreamResult, TResult> createResultFunction)
    {
        using var decryptingStream = streamGetter.Invoke(streamResult);
        using var streamReader = new StreamReader(decryptingStream, Encoding.UTF8);

        return createResultFunction.Invoke(streamReader.ReadToEnd(), streamResult);
    }
}
