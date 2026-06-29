using Proton.Drive.Shared.Client;
using Refit;

namespace Proton.Drive.Sdk.Sync.Client.TlsPinning.Reporting;

internal interface ITlsPinningReportApiClient
{
    [Post("/v4/reports/tls")]
    public Task<ApiResponse> SendAsync([Body] TlsPinningReport report, CancellationToken cancellationToken);
}
