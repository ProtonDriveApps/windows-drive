using Proton.Drive.Shared.Net.Http.TlsPinning;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Client.TlsPinning.Reporting;

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
