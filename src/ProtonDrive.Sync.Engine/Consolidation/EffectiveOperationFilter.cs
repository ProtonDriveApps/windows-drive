using System;
using System.Diagnostics.CodeAnalysis;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Engine.Consolidation;

internal class EffectiveOperationFilter<TId>
    where TId : IEquatable<TId>
{
    private readonly FileSystemNodeModelLinkEqualityComparer<TId> _nodeLinkComparer = new();
    private readonly FileSystemNodeModelAttributesEqualityComparer<TId> _nodeAttributesComparer = new();
    private readonly UpdateTreeNodeModelMetadataEqualityComparer<TId> _nodeMetadataComparer = new();

    public bool HasEffect(
        [NotNullWhen(true)]
        Operation<UpdateTreeNodeModel<TId>>? operation,
        UpdateTreeNodeModel<TId>? nodeModel)
    {
        if (operation == null)
        {
            return false;
        }

        if (nodeModel == null)
        {
            return true;
        }

        switch (operation.Type)
        {
            case OperationType.Edit:
                return !_nodeAttributesComparer.Equals(operation.Model, nodeModel)
                       || !_nodeMetadataComparer.Equals(operation.Model, nodeModel);

            case OperationType.Move:
                return !_nodeLinkComparer.Equals(operation.Model, nodeModel)
                       || !_nodeMetadataComparer.Equals(operation.Model, nodeModel);

            case OperationType.Update:
                return !_nodeMetadataComparer.Equals(operation.Model, nodeModel);

            default:
                return true;
        }
    }
}
