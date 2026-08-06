using Proton.Drive.Sdk.Sync.Client.Events;

namespace Proton.Drive.Sdk.Sync.Client.Shares.Events;

internal interface IShareEventClient
{
    Task<DriveEvents> GetEventsAsync(string shareId, DriveEventResumeToken resumeToken, CancellationToken cancellationToken);
}
