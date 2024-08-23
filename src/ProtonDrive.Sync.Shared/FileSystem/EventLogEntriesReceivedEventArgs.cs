using System;
using System.Collections.Generic;

namespace ProtonDrive.Sync.Shared.FileSystem;

public sealed class EventLogEntriesReceivedEventArgs<TId> : EventArgs
{
    private readonly Action _onEventsProcessedAction;

    public EventLogEntriesReceivedEventArgs(
        IReadOnlyCollection<EventLogEntry<TId>> entries,
        Action? onEventsProcessedAction = default)
    {
        _onEventsProcessedAction = onEventsProcessedAction ?? (static () => { });

        Entries = entries;
    }

    public IReadOnlyCollection<EventLogEntry<TId>> Entries { get; }
    public int VolumeId { get; set; }
    public string Scope { get; set; } = string.Empty;

    public void ConsiderEventsProcessed()
    {
        _onEventsProcessedAction.Invoke();
    }
}
