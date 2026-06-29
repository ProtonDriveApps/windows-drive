using Proton.Drive.Shared.Client;
using Refit;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Telemetry;

public interface ITelemetryApiClient
{
    [Post("/v1/stats/multiple")]
    [BearerAuthorizationHeader]
    Task<ApiResponse> SendEventsAsync(TelemetryEvents telemetryEvents, CancellationToken cancellationToken);
}
