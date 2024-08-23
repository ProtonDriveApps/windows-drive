using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Client.Core.Events;

public interface ICoreEventClient
{
    Task<CoreEvents> GetEventsAsync(CoreEventResumeToken resumeToken, CancellationToken cancellationToken);
}
