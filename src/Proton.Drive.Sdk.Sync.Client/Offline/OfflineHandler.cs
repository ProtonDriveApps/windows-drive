using Polly;

namespace Proton.Drive.Sdk.Sync.Client.Offline;

internal sealed class OfflineHandler : DelegatingHandler
{
    private readonly ResiliencePipeline<HttpResponseMessage> _policy;

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
        return await _policy.ExecuteAsync(InternalSendAsync, cancellationToken).ConfigureAwait(false);

        async ValueTask<HttpResponseMessage> InternalSendAsync(CancellationToken internalCancellationToken)
        {
            return await base.SendAsync(request, internalCancellationToken).ConfigureAwait(false);
        }
    }
}
