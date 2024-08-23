using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Contracts;
using Refit;

namespace ProtonDrive.Client.Volumes.Events;

internal interface IVolumeEventApiClient
{
    [Get("/volumes/{volumeId}/events/latest")]
    [BearerAuthorizationHeader]
    Task<LatestEventResponse> GetLatestEventAsync(string volumeId, CancellationToken cancellationToken);

    [Get("/volumes/{volumeId}/events/{anchorId}")]
    [BearerAuthorizationHeader]
    Task<EventListResponse> GetEventsAsync(string volumeId, string anchorId, CancellationToken cancellationToken);
}
