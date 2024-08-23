using System.Threading;
using System.Threading.Tasks;
using Refit;

namespace ProtonDrive.Client.TlsPinning.Reporting;

internal interface ITlsPinningReportApiClient
{
    [Post("/v4/reports/tls")]
    public Task<ApiResponse> SendAsync([Body] TlsPinningReport report, CancellationToken cancellationToken);
}
