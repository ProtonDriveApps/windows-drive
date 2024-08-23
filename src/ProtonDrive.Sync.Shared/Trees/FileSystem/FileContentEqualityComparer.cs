using System;
using System.Collections.Generic;

namespace ProtonDrive.Sync.Shared.Trees.FileSystem;

public class FileContentEqualityComparer<TId> : IEqualityComparer<FileSystemNodeModel<TId>>
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

        // The content of two files is considered equal if files have equal content version.
        return x.ContentVersion == y.ContentVersion;
    }

    public int GetHashCode(FileSystemNodeModel<TId> obj)
    {
        throw new NotSupportedException();
    }
}
