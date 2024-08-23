using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Client.Configuration;

internal sealed class ChunkedTransferEncodingHandler : DelegatingHandler
{
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is not null && request.Content.Headers.ContentLength is null)
        {
            await request.Content.LoadIntoBufferAsync().ConfigureAwait(false);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
