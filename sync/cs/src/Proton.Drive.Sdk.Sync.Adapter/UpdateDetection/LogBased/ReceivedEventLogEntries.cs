using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Adapter.UpdateDetection.LogBased;

internal sealed record ReceivedEventLogEntries<T>(
    IReadOnlyCollection<EventLogEntry<T>> Entries,
    int VolumeId,
    string Scope,
    long Timestamp,
    Action OnProcessed);
