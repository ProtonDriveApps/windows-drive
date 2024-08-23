using System;
using System.Collections.Generic;
using Proton.Security.Cryptography.Abstractions;
using Proton.Security.Cryptography.GopenPgp;

namespace ProtonDrive.Client.Cryptography;

internal sealed class PgpTransformerFactory : IPgpTransformerFactory
{
    public IPgpMessageProducer CreateMessageProducingEncrypter(PublicPgpKey publicKey, Func<DateTimeOffset> getTimestampFunction)
    {
        return new KeyBasedPgpMessageProducer(publicKey, getTimestampFunction);
    }

    public ISigningCapablePgpMessageProducer CreateMessageAndSignatureProducingEncrypter(
        PublicPgpKey publicKey,
        PrivatePgpKey signaturePrivateKey,
        Func<DateTimeOffset> getTimestampFunction)
    {
        return new SigningCapablePgpMessageProducer(publicKey, signaturePrivateKey, getTimestampFunction);
    }

    public ISigningCapablePgpMessageProducer CreateMessageAndSignatureProducingEncrypter(
        PublicPgpKey publicKey,
        PgpSessionKey sessionKey,
        PrivatePgpKey signaturePrivateKey,
        Func<DateTimeOffset> getTimestampFunction)
    {
        return new SigningCapablePgpMessageProducer(publicKey, sessionKey, signaturePrivateKey, getTimestampFunction);
    }

    public IPgpDecrypter CreateDecrypter(IReadOnlyCollection<PrivatePgpKey> privateKeyRing)
    {
        return new KeyBasedPgpDecrypter(privateKeyRing);
    }

    public IVerificationCapablePgpDecrypter CreateVerificationCapableDecrypter(
        IReadOnlyCollection<PrivatePgpKey> privateKeyRing,
        IReadOnlyCollection<PublicPgpKey> verificationPublicKeyRing)
    {
        return new VerificationCapablePgpDecrypter(privateKeyRing, verificationPublicKeyRing);
    }
}
