using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Events;

namespace ProtonDrive.Client.Volumes.Events;

internal interface IVolumeEventClient
{
    Task<DriveEvents> GetEventsAsync(string volumeId, DriveEventResumeToken resumeToken, CancellationToken cancellationToken);
}
