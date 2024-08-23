using System;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Shared.Trees.Operations;

public class ExecutableOperation<TId> : Operation<AltIdentifiableFileSystemNodeModel<TId, TId>>
    where TId : IEquatable<TId>
{
    public ExecutableOperation(OperationType type, AltIdentifiableFileSystemNodeModel<TId, TId> model, bool backup)
        : base(type, model)
    {
        Backup = backup;
    }

    public bool Backup { get; }
}
