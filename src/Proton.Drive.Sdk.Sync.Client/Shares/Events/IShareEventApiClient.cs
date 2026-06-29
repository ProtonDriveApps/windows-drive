using Proton.Drive.Sdk.Sync.Client.Contracts;
using Refit;

namespace Proton.Drive.Sdk.Sync.Client.Shares.Events;

internal interface IShareEventApiClient
{
    [Get("/shares/{shareId}/events/latest")]
    [BearerAuthorizationHeader]
    Task<LatestEventResponse> GetLatestEventAsync(string shareId, CancellationToken cancellationToken);

    [Get("/shares/{shareId}/events/{anchorId}")]
    [BearerAuthorizationHeader]
    Task<EventListResponse> GetEventsAsync(string shareId, string anchorId, CancellationToken cancellationToken);
}
