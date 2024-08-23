using System.Collections.Generic;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem;

internal interface IRootDeletionDetector<TId>
{
    void HandleEventLogEntries(int volumeId, IReadOnlyCollection<EventLogEntry<TId>> entries);
}
