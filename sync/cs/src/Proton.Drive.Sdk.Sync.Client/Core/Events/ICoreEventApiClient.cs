using Proton.Drive.Sdk.Sync.Client.Core.Events.Contracts;
using Refit;

namespace Proton.Drive.Sdk.Sync.Client.Core.Events;

internal interface ICoreEventApiClient
{
    [Get("/v4/events/latest")]
    [BearerAuthorizationHeader]
    Task<CoreLatestEventResponse> GetLatestEventAsync(CancellationToken cancellationToken);

    [Get("/v5/events/{anchorId}")]
    [BearerAuthorizationHeader]
    Task<CoreEventListResponse> GetEventsAsync(string anchorId, CancellationToken cancellationToken);
}
