using System;
using System.Threading.Tasks;
using ProtonDrive.Shared.Net.Http.TlsPinning;

namespace ProtonDrive.Client.TlsPinning.Reporting;

internal class SafeTlsPinningReportClient : ITlsPinningReportClient
{
    private readonly ITlsPinningReportClient _origin;

    public SafeTlsPinningReportClient(ITlsPinningReportClient origin)
    {
        _origin = origin;
    }

    public Task SendAsync(TlsPinningReportContent content)
    {
        return Safe(() => _origin.SendAsync(content));
    }

    private async Task Safe(Func<Task> origin)
    {
        try
        {
            await origin().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ApiException or OperationCanceledException)
        {
            // Ignore
        }
    }
}
