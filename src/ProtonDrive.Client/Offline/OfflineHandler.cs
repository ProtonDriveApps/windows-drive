using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Polly;

namespace ProtonDrive.Client.Offline;

internal sealed class OfflineHandler : DelegatingHandler
{
    private readonly AsyncPolicy<HttpResponseMessage> _policy;

    public OfflineHandler(IOfflinePolicyProvider provider)
    {
        _policy = provider.GetPolicy();
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await _policy.ExecuteAsync(() => base.SendAsync(request, cancellationToken)).ConfigureAwait(false);
    }
}
