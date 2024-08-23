using System;
using System.Collections.Generic;

namespace ProtonDrive.Sync.Engine.Shared.Trees.Propagation;

internal class PropagationTreeNodeModelMetadataEqualityComparer<TId> : IEqualityComparer<PropagationTreeNodeModel<TId>>
    where TId : IEquatable<TId>
{
    public bool Equals(PropagationTreeNodeModel<TId>? x, PropagationTreeNodeModel<TId>? y)
    {
        if (x is null || y is null)
        {
            return false;
        }

        if (ReferenceEquals(x, y))
        {
            return true;
        }

        return ((x.AltId is null && y.AltId is null) || (x.AltId?.Equals(y.AltId) ?? false)) &&
               x.RemoteStatus == y.RemoteStatus &&
               x.LocalStatus == y.LocalStatus &&
               x.Backup == y.Backup;
    }

    public int GetHashCode(PropagationTreeNodeModel<TId> obj)
    {
        throw new NotImplementedException();
    }
}
