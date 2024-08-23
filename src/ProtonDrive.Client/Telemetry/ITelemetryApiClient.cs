using System.Threading;
using System.Threading.Tasks;
using Refit;

namespace ProtonDrive.Client.Telemetry;

public interface ITelemetryApiClient
{
    [Post("/v1/stats")]
    [BearerAuthorizationHeader]
    Task<ApiResponse> SendEventAsync(TelemetryEvent telemetryEvent, CancellationToken cancellationToken);

    [Post("/v1/stats/multiple")]
    [BearerAuthorizationHeader]
    Task<ApiResponse> SendEventsAsync(TelemetryEvents telemetryEvents, CancellationToken cancellationToken);
}
