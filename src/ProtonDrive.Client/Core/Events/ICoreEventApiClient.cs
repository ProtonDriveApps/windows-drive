using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Core.Events.Contracts;
using Refit;

namespace ProtonDrive.Client.Core.Events;

internal interface ICoreEventApiClient
{
    [Get("/v4/events/latest")]
    [BearerAuthorizationHeader]
    Task<CoreLatestEventResponse> GetLatestEventAsync(CancellationToken cancellationToken);

    [Get("/v5/events/{anchorId}")]
    [BearerAuthorizationHeader]
    Task<CoreEventListResponse> GetEventsAsync(string anchorId, CancellationToken cancellationToken);
}
