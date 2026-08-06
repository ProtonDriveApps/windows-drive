using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;

namespace Proton.Drive.Sdk.Sync.Shared.Trees.Operations;

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
