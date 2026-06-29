using System.Buffers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Shared.Net.Http.TlsPinning;

public sealed class TlsPinningPolicy
{
    private readonly IReadOnlyCollection<byte[]> _knownPublicKeyHashDigests;

    public TlsPinningPolicy(TlsPinningConfig config)
    {
        _knownPublicKeyHashDigests = config.PublicKeyHashes.Select(Convert.FromBase64String).AsReadOnlyCollection(config.PublicKeyHashes.Count);
    }

    public bool IsValid(X509Certificate certificate)
    {
        using var certificate2 = new X509Certificate2(certificate);
        Span<byte> hashDigestBuffer = stackalloc byte[SHA256.HashSizeInBytes];
        if (!TryGetPublicKeySha256Digest(certificate2, hashDigestBuffer))
        {
            return false;
        }

        foreach (var knownPublicKeyHashDigest in _knownPublicKeyHashDigests)
        {
            if (knownPublicKeyHashDigest.AsSpan().SequenceEqual(hashDigestBuffer))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetPublicKeySha256Digest(X509Certificate2 certificate, Span<byte> outputBuffer)
    {
        var publicKey =
            (AsymmetricAlgorithm?)certificate.GetRSAPublicKey()
            ?? (AsymmetricAlgorithm?)certificate.GetDSAPublicKey()
            ?? (AsymmetricAlgorithm?)certificate.GetECDiffieHellmanPublicKey()
            ?? (AsymmetricAlgorithm?)certificate.GetECDsaPublicKey()
            ?? throw new NotSupportedException("No supported key algorithm");

        // Expected length of public key info is around 550 bytes
        var publicKeyInfoBuffer = ArrayPool<byte>.Shared.Rent(1024);

        try
        {
            var publishKeyInfo = publicKey.TryExportSubjectPublicKeyInfo(publicKeyInfoBuffer, out var publicKeyInfoLength)
                ? publicKeyInfoBuffer.AsSpan()[..publicKeyInfoLength]
                : publicKey.ExportSubjectPublicKeyInfo();

            return SHA256.TryHashData(publishKeyInfo, outputBuffer, out _);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(publicKeyInfoBuffer);
        }
    }
}
