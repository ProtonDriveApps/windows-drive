using System;
using System.Collections.Generic;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Adapter.Trees.Adapter;

namespace ProtonDrive.Sync.Adapter.UpdateDetection;

internal class FileVersionMapping<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly Dictionary<TId, AdapterTreeNodeModel<TId, TAltId>> _mappings = new();

    public void Add(AdapterTreeNodeModel<TId, TAltId> nodeModel)
    {
        _mappings[nodeModel.Id] = nodeModel.Copy();
    }

    public long GetVersion(TId id, DateTime lastWriteTime, long size)
    {
        if (!_mappings.TryGetValue(id, out var value))
        {
            return default;
        }

        if (lastWriteTime == value.LastWriteTime &&
            size == value.Size)
        {
            // Once the file last write time and size reaches the expected values,
            // they do not change anymore. The mapping can be removed after using
            // it once.
            _mappings.Remove(id);

            return value.ContentVersion;
        }

        return default;
    }

    // TODO: Remove outdated mappings that were not matched against the file system event-log entries.
}
