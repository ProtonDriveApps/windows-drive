using Proton.Drive.Sdk.Sync.Client.Events;

namespace Proton.Drive.Sdk.Sync.Client.Volumes.Events;

internal interface IVolumeEventClient
{
    Task<DriveEvents> GetEventsAsync(string volumeId, DriveEventResumeToken resumeToken, CancellationToken cancellationToken);
}
