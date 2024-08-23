using System;
using System.Collections.Generic;

namespace ProtonDrive.Sync.Engine.Shared.Trees.Update;

internal class UpdateTreeNodeModelMetadataEqualityComparer<TId> : IEqualityComparer<UpdateTreeNodeModel<TId>>
    where TId : IEquatable<TId>
{
    public bool Equals(UpdateTreeNodeModel<TId>? x, UpdateTreeNodeModel<TId>? y)
    {
        if (x is null || y is null)
        {
            return false;
        }

        if (ReferenceEquals(x, y))
        {
            return true;
        }

        return x.Status == y.Status;
    }

    public int GetHashCode(UpdateTreeNodeModel<TId> obj)
    {
        throw new NotImplementedException();
    }
}
