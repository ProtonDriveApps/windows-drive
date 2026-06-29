using Proton.Cryptography.Pgp;
using Proton.Drive.Sdk.Sync.Client.Cryptography.Pgp;

namespace Proton.Drive.Sdk.Sync.Client.Cryptography;

internal sealed class PgpTransformerFactory : IPgpTransformerFactory
{
    public ISigningCapablePgpMessageProducer CreateMessageAndSignatureProducingEncrypter(
        PgpPublicKey publicKey,
        PgpPrivateKey signaturePrivateKey)
    {
        return new SigningCapablePgpMessageProducer(publicKey, signaturePrivateKey);
    }

    public ISigningCapablePgpMessageProducer CreateMessageAndSignatureProducingEncrypter(
        PgpPublicKey publicKey,
        PgpSessionKey sessionKey,
        PgpPrivateKey signaturePrivateKey)
    {
        return new SigningCapablePgpMessageProducer(publicKey, sessionKey, signaturePrivateKey);
    }

    public IPgpDecrypter CreateDecrypter(IReadOnlyList<PgpPrivateKey> privateKeyRing)
    {
        return new KeyBasedPgpDecrypter(privateKeyRing);
    }

    public IVerificationCapablePgpDecrypter CreateVerificationCapableDecrypter(
        IReadOnlyList<PgpPrivateKey> privateKeyRing,
        IReadOnlyList<PgpPublicKey> verificationPublicKeyRing)
    {
        return new VerificationCapablePgpDecrypter(privateKeyRing, verificationPublicKeyRing);
    }
}
