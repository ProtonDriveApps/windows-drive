using System;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Shared.Trees.Changes;

public interface IDetectedTreeChanges<TId>
    where TId : IEquatable<TId>
{
    void Add(Operation<FileSystemNodeModel<TId>> operation);
    bool Contains(TId id);
}
