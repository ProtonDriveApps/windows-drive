using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared.Net.Http.TlsPinning;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.Client.TlsPinning.Reporting;

internal class TlsPinningReportClient : ITlsPinningReportClient
{
    private readonly ITlsPinningReportApiClient _apiClient;
    private readonly IScheduler _scheduler = new SerialScheduler();

    public TlsPinningReportClient(ITlsPinningReportApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task SendAsync(TlsPinningReportContent content)
    {
        var report = new TlsPinningReport(content);
        await SendAsync(report).ConfigureAwait(false);
    }

    private Task SendAsync(TlsPinningReport report)
    {
        return _scheduler.Schedule(() => _apiClient.SendAsync(report, CancellationToken.None).ThrowOnFailure());
    }
}
