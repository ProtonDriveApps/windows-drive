using System;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Shared.Trees.Changes;

public class TreeChange<TId>
    where TId : IEquatable<TId>
{
    public TreeChange(TId id, Operation<FileSystemNodeModel<TId>> operation)
    {
        Id = id;
        Operation = operation;
    }

    public TId Id { get; }
    public Operation<FileSystemNodeModel<TId>> Operation { get; }
}
