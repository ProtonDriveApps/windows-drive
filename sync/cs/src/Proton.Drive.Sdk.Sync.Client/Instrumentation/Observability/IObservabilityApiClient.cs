using Proton.Drive.Shared.Client;
using Refit;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability;

public interface IObservabilityApiClient
{
    [Post("/v1/metrics")]
    [BearerAuthorizationHeader]
    Task<ApiResponse> SendMetricsAsync(ObservabilityMetrics metrics, CancellationToken cancellationToken);
}
