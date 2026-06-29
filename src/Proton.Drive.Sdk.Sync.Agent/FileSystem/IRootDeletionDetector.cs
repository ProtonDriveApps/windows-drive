using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem;

internal interface IRootDeletionDetector<TId>
{
    void HandleEventLogEntries(int volumeId, IReadOnlyCollection<EventLogEntry<TId>> entries);
}
