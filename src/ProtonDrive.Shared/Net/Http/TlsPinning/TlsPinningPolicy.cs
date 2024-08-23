using System.Security.Cryptography.X509Certificates;

namespace ProtonDrive.Shared.Net.Http.TlsPinning;

internal sealed class TlsPinningPolicy
{
    private readonly TlsPinningConfig _config;

    public TlsPinningPolicy(TlsPinningConfig config)
    {
        _config = config;
    }

    public bool IsValid(X509Certificate certificate)
    {
        using var certificate2 = new X509Certificate2(certificate);
        var hash = new PublicKeyInfoHash(certificate2).Value();

        return _config.PublicKeyHashes.Contains(hash);
    }
}
