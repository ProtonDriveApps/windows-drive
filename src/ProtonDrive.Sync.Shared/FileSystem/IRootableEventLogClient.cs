using System;

namespace ProtonDrive.Sync.Shared.FileSystem;

public interface IRootableEventLogClient<TId>
{
    event EventHandler<EventLogEntriesReceivedEventArgs<TId>> LogEntriesReceived;

    void Enable(IRootDirectory<TId> rootDirectory);
    void Disable();
}
