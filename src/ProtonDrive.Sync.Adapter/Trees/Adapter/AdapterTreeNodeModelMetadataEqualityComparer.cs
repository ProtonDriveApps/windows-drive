using System;
using System.Collections.Generic;

namespace ProtonDrive.Sync.Adapter.Trees.Adapter;

internal class AdapterTreeNodeModelMetadataEqualityComparer<TId, TAltId> : IEqualityComparer<AdapterTreeNodeModel<TId, TAltId>>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    public bool Equals(AdapterTreeNodeModel<TId, TAltId>? x, AdapterTreeNodeModel<TId, TAltId>? y)
    {
        if (x is null || y is null)
        {
            return false;
        }

        if (ReferenceEquals(x, y))
        {
            return true;
        }

        return x.AltId.Equals(y.AltId) &&
               x.RevisionId == y.RevisionId &&
               x.LastWriteTime == y.LastWriteTime &&
               x.Size == y.Size &&
               x.Status == y.Status;
    }

    public int GetHashCode(AdapterTreeNodeModel<TId, TAltId> obj)
    {
        throw new NotSupportedException();
    }
}
