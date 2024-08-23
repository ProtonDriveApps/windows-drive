using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Client.Configuration;

internal sealed class ServerTimeRecordingHandler : DelegatingHandler
{
    private readonly ServerTimeCache _serverTimeCache;

    public ServerTimeRecordingHandler(ServerTimeCache serverTimeCache)
    {
        _serverTimeCache = serverTimeCache;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var responseMessage = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (responseMessage.Headers.Date is { } time)
        {
            _serverTimeCache.ServerTime = time;
        }

        return responseMessage;
    }
}
