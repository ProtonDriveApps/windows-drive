using System;
using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using ProtonDrive.Shared.Net.Http.TlsPinning;

namespace ProtonDrive.Client.TlsPinning.Reporting;

internal class CachingTlsPinningReportClient : ITlsPinningReportClient
{
    private readonly ConcurrentDictionary<int, Void> _cache = new();

    private readonly ITlsPinningReportClient _origin;

    public CachingTlsPinningReportClient(ITlsPinningReportClient origin)
    {
        _origin = origin;
    }

    public async Task SendAsync(TlsPinningReportContent content)
    {
        var hash = GetHashCode(content.CertificateChain);
        if (_cache.TryAdd(hash, default))
        {
            await _origin.SendAsync(content).ConfigureAwait(false);
        }
    }

    private int GetHashCode(X509Chain chain)
    {
        var hash = default(HashCode);

        foreach (var element in chain.ChainElements)
        {
            hash.Add(element.Certificate.Thumbprint);
        }

        return hash.ToHashCode();
    }

    private struct Void { }
}
