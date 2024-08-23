using System;
using System.Collections.Generic;
using Proton.Security.Cryptography.Abstractions;

namespace ProtonDrive.Client.Cryptography;

public interface IPgpTransformerFactory
{
    IPgpMessageProducer CreateMessageProducingEncrypter(PublicPgpKey publicKey, Func<DateTimeOffset> getTimestampFunction);

    ISigningCapablePgpMessageProducer CreateMessageAndSignatureProducingEncrypter(
        PublicPgpKey publicKey,
        PrivatePgpKey signaturePrivateKey,
        Func<DateTimeOffset> getTimestampFunction);

    ISigningCapablePgpMessageProducer CreateMessageAndSignatureProducingEncrypter(
        PublicPgpKey publicKey,
        PgpSessionKey sessionKey,
        PrivatePgpKey signaturePrivateKey,
        Func<DateTimeOffset> getTimestampFunction);

    IPgpDecrypter CreateDecrypter(IReadOnlyCollection<PrivatePgpKey> privateKeyRing);

    IVerificationCapablePgpDecrypter CreateVerificationCapableDecrypter(
        IReadOnlyCollection<PrivatePgpKey> privateKeyRing,
        IReadOnlyCollection<PublicPgpKey> verificationPublicKeyRing);
}
