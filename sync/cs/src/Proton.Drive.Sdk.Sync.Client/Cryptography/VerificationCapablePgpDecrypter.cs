using CommunityToolkit.HighPerformance;
using Proton.Cryptography.Pgp;
using Proton.Drive.Sdk.Sync.Client.Cryptography.Pgp;

namespace Proton.Drive.Sdk.Sync.Client.Cryptography;

internal sealed class VerificationCapablePgpDecrypter(IReadOnlyList<PgpPrivateKey> privateKeyRing)
    : KeyBasedPgpDecrypter(privateKeyRing), IVerificationCapablePgpDecrypter
{
    private readonly PgpKeyRing _verificationPublicKeyRing;

    public VerificationCapablePgpDecrypter(
        IReadOnlyList<PgpPrivateKey> privateKeyRing,
        IReadOnlyList<PgpPublicKey> verificationPublicKeyRing)
        : this(privateKeyRing)
    {
        _verificationPublicKeyRing = new PgpKeyRing(verificationPublicKeyRing);
    }

    public (Stream DecryptingStream, Func<PgpVerificationStatus> GetVerificationStatus) GetDecryptingAndVerifyingStream(ReadOnlyMemory<byte> armoredMessage)
    {
        var stream = PgpPrivateKeyRing.OpenDecryptingAndVerifyingStream(armoredMessage.AsStream(), _verificationPublicKeyRing, PgpEncoding.AsciiArmor);
        return (stream, () => stream.GetVerificationResult().Status);
    }

    public DecryptingAndVerifyingStreamWithSessionKeyProvisionResult GetDecryptingAndVerifyingStreamWithSessionKey(ReadOnlyMemory<byte> armoredMessage)
    {
        var message = PgpArmorDecoder.Decode(armoredMessage.Span);
        var sessionKey = PgpPrivateKeyRing.DecryptSessionKey(message);
        var stream = sessionKey.OpenDecryptingAndVerifyingStream(armoredMessage.AsStream(), _verificationPublicKeyRing, PgpEncoding.AsciiArmor);
        return new DecryptingAndVerifyingStreamWithSessionKeyProvisionResult(stream, sessionKey, () => stream.GetVerificationResult().Status);
    }

    public DecryptingAndVerifyingStreamWithSessionKeyProvisionResult GetDecryptingAndVerifyingStreamWithSessionKey(
        ReadOnlyMemory<byte> armoredMessage,
        ReadOnlyMemory<byte> detachedSignatureSource)
    {
        var message = PgpArmorDecoder.Decode(armoredMessage.Span);
        var sessionKey = PgpPrivateKeyRing.DecryptSessionKey(message);
        var stream = sessionKey.OpenDecryptingAndVerifyingStream(
            armoredMessage.AsStream(),
            detachedSignatureSource,
            _verificationPublicKeyRing,
            PgpEncoding.AsciiArmor,
            signatureEncoding: PgpEncoding.AsciiArmor);

        return new DecryptingAndVerifyingStreamWithSessionKeyProvisionResult(stream, sessionKey, () => stream.GetVerificationResult().Status);
    }

    public PgpVerificationStatus Verify(ReadOnlySpan<byte> plainData, ReadOnlySpan<byte> armoredSignature)
    {
        return _verificationPublicKeyRing.Verify(plainData, armoredSignature, PgpEncoding.AsciiArmor).Status;
    }
}
