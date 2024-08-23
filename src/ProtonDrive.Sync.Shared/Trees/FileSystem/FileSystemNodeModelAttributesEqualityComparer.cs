using System;
using System.Collections.Generic;

namespace ProtonDrive.Sync.Shared.Trees.FileSystem;

public class FileSystemNodeModelAttributesEqualityComparer<TId> : IEqualityComparer<FileSystemNodeModel<TId>>
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

        return x.Type == y.Type
               && x.ContentVersion == y.ContentVersion;
    }

    public int GetHashCode(FileSystemNodeModel<TId> obj)
    {
        throw new NotImplementedException();
    }
}
