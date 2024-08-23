using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ProtonDrive.Shared.Net.Http.TlsPinning;

internal sealed class PublicKeyInfoHash
{
    private readonly X509Certificate2 _certificate;

    public PublicKeyInfoHash(X509Certificate2 certificate)
    {
        _certificate = certificate;
    }

    public string Value()
    {
        var publicKey = (AsymmetricAlgorithm?)_certificate.GetRSAPublicKey()
                        ?? _certificate.GetDSAPublicKey()
                        ?? throw new NotSupportedException("No supported key algorithm");

        var publicKeyInfo = publicKey.ExportSubjectPublicKeyInfo();
        byte[] hash;
        using (var sha256 = SHA256.Create())
        {
            hash = sha256.ComputeHash(publicKeyInfo);
        }

        return Convert.ToBase64String(hash);
    }
}
