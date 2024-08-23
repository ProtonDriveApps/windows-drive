using System;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Engine.Consolidation;

internal class ConsolidationOperationFactory<TId>
    where TId : IEquatable<TId>
{
    private readonly Replica _replica;
    private readonly ConsolidatedUpdateStatus<TId> _consolidatedUpdateStatus;

    public ConsolidationOperationFactory(Replica replica)
    {
        _replica = replica;

        _consolidatedUpdateStatus = new ConsolidatedUpdateStatus<TId>(replica);
    }

    public Operation<UpdateTreeNodeModel<TId>>? Operation(
        Operation<FileSystemNodeModel<TId>> operation,
        UpdateTreeNodeModel<TId>? nodeModel,
        SyncedTreeNode<TId>? syncedNode)
    {
        var status = _consolidatedUpdateStatus.Consolidated(operation, nodeModel, syncedNode);

        if (status.Contains(UpdateStatus.Deleted) && nodeModel == null && syncedNode == null)
        {
            return null;
        }

        if (status == UpdateStatus.Unchanged && nodeModel == null)
        {
            return null;
        }

        var model = new UpdateTreeNodeModel<TId>();
        if (operation.Type == OperationType.Create)
        {
            model = model.CopiedFrom(operation.Model);
        }
        else
        {
            model = nodeModel != null
                ? model.CopiedFrom(nodeModel)
                : model.CopiedFrom(syncedNode!.Model)
                    .WithId(operation.Model.Id)
                    .WithParentId(syncedNode.Parent!.Model.OwnId(_replica));

            switch (operation.Type)
            {
                case OperationType.Edit:
                    model = model.WithAttributesFrom(operation.Model);
                    break;
                case OperationType.Move:
                    model = model.WithLinkFrom(operation.Model);
                    break;
            }
        }

        model = model.WithStatus(status);

        var operationType = operation.Type;
        if (nodeModel == null)
        {
            operationType = OperationType.Create;
        }
        else if (status.Contains(UpdateStatus.Deleted) ||
                 (status == UpdateStatus.Created && operationType == OperationType.Create))
        {
            operationType = OperationType.Update;
        }

        return new Operation<UpdateTreeNodeModel<TId>>(operationType, model);
    }
}
