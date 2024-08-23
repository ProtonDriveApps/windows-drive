using System;
using System.Threading.Tasks;

namespace ProtonDrive.Sync.Shared.FileSystem;

public interface IEventLogClient<TId>
{
    event EventHandler<EventLogEntriesReceivedEventArgs<TId>> LogEntriesReceived;

    void Enable();
    void Disable();
    Task GetEventsAsync();
}
