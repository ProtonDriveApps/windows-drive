using System;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Shared.Trees.Operations;

public static class ExecutableOperationExtensions
{
    public static bool IsFileTransfer<TModel>(this ExecutableOperation<TModel> operation)
        where TModel : IEquatable<TModel>
    {
        return (operation.Type is OperationType.Create && operation.Model.Type == NodeType.File) ||
                operation.Type is OperationType.Edit;
    }
}
