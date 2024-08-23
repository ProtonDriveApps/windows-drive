using System;
using System.Collections.Generic;

namespace ProtonDrive.Sync.Shared.Trees.FileSystem;

public class FileSystemNodeModelLinkEqualityComparer<TId> : IEqualityComparer<FileSystemNodeModel<TId>>
    where TId : IEquatable<TId>
{
    public bool Equals(FileSystemNodeModel<TId>? x, FileSystemNodeModel<TId>? y)
    {
        if (x is null)
        {
            return false;
        }

        if (y is null)
        {
            return false;
        }

        if (ReferenceEquals(x, y))
        {
            return true;
        }

        return x.ParentId.Equals(y.ParentId) &&
               x.Name == y.Name;
    }

    public int GetHashCode(FileSystemNodeModel<TId> obj)
    {
        throw new NotImplementedException();
    }
}
