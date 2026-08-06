namespace Proton.Drive.Sdk.Sync.Shared.FileSystem;

public interface IEventLogClient<TId>
{
    event EventHandler<EventLogEntriesReceivedEventArgs<TId>> LogEntriesReceived;

    void Enable();
    void Disable();
    Task GetEventsAsync();
}
