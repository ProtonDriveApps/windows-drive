using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Contracts;
using Refit;

namespace ProtonDrive.Client.Shares.Events;

internal interface IShareEventApiClient
{
    [Get("/shares/{shareId}/events/latest")]
    [BearerAuthorizationHeader]
    Task<LatestEventResponse> GetLatestEventAsync(string shareId, CancellationToken cancellationToken);

    [Get("/shares/{shareId}/events/{anchorId}")]
    [BearerAuthorizationHeader]
    Task<EventListResponse> GetEventsAsync(string shareId, string anchorId, CancellationToken cancellationToken);
}
