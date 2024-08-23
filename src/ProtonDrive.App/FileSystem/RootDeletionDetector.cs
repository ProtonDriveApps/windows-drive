using System.Collections.Generic;
using System.Linq;
using ProtonDrive.App.Mapping;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem;

internal sealed class RootDeletionDetector<TId> : IRootDeletionDetector<TId>
{
    private readonly IRootDeletionHandler _deletionHandler;
    private readonly int _volumeId;
    private readonly IReadOnlyDictionary<TId, IReadOnlyCollection<int>> _nodeIdToRootMap;

    public RootDeletionDetector(IRootDeletionHandler deletionHandler, int volumeId, IReadOnlyDictionary<TId, IReadOnlyCollection<int>> nodeIdToRootMap)
    {
        _deletionHandler = deletionHandler;
        _volumeId = volumeId;
        _nodeIdToRootMap = nodeIdToRootMap;
    }

    public void HandleEventLogEntries(int volumeId, IReadOnlyCollection<EventLogEntry<TId>> entries)
    {
        if (volumeId != _volumeId || entries.Count == 0)
        {
            return;
        }

        var affectedRootIds = entries
            .Where(e => e.ChangeType is EventLogChangeType.Deleted or EventLogChangeType.DeletedOrMovedFrom)
            .SelectMany(e => _nodeIdToRootMap.TryGetValue(e.Id!, out var rootIds) ? rootIds : [])
            .ToHashSet();

        if (affectedRootIds.Count != 0)
        {
            _deletionHandler.HandleRootDeletion(affectedRootIds);
        }
    }
}
