using Proton.Cryptography.Pgp;
using Proton.Drive.Sdk.Sync.Client.Cryptography.Pgp;

namespace Proton.Drive.Sdk.Sync.Client.Cryptography;

internal interface IPgpTransformerFactory
{
    ISigningCapablePgpMessageProducer CreateMessageAndSignatureProducingEncrypter(
        PgpPublicKey publicKey,
        PgpPrivateKey signaturePrivateKey);

    ISigningCapablePgpMessageProducer CreateMessageAndSignatureProducingEncrypter(
        PgpPublicKey publicKey,
        PgpSessionKey sessionKey,
        PgpPrivateKey signaturePrivateKey);

    IPgpDecrypter CreateDecrypter(IReadOnlyList<PgpPrivateKey> privateKeyRing);

    IVerificationCapablePgpDecrypter CreateVerificationCapableDecrypter(
        IReadOnlyList<PgpPrivateKey> privateKeyRing,
        IReadOnlyList<PgpPublicKey> verificationPublicKeyRing);
}
