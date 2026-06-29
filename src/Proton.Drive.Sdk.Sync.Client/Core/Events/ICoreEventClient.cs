namespace Proton.Drive.Sdk.Sync.Client.Core.Events;

public interface ICoreEventClient
{
    Task<CoreEvents> GetEventsAsync(CoreEventResumeToken resumeToken, CancellationToken cancellationToken);
}
