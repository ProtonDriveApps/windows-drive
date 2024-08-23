using System;
using System.Collections.Generic;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Sync.Adapter.UpdateDetection.LogBased;

internal sealed record ReceivedEventLogEntries<T>(
    IReadOnlyCollection<EventLogEntry<T>> Entries,
    int VolumeId,
    string Scope,
    long Timestamp,
    Action OnProcessed);
