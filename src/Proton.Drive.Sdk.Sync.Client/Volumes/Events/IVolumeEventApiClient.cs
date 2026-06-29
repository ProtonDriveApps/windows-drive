using Proton.Drive.Sdk.Sync.Client.Contracts;
using Refit;

namespace Proton.Drive.Sdk.Sync.Client.Volumes.Events;

internal interface IVolumeEventApiClient
{
    [Get("/volumes/{volumeId}/events/latest")]
    [BearerAuthorizationHeader]
    Task<LatestEventResponse> GetLatestEventAsync(string volumeId, CancellationToken cancellationToken);

    [Get("/volumes/{volumeId}/events/{anchorId}")]
    [BearerAuthorizationHeader]
    Task<EventListResponse> GetEventsAsync(string volumeId, string anchorId, CancellationToken cancellationToken);
}
