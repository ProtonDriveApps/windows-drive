using System;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Engine.Consolidation;

internal class ConsolidatedUpdateStatus<TId>
    where TId : IEquatable<TId>
{
    private readonly Replica _replica;
    private readonly FileContentEqualityComparer<TId> _contentEqualityComparer;

    public ConsolidatedUpdateStatus(Replica replica)
    {
        _replica = replica;

        _contentEqualityComparer = new FileContentEqualityComparer<TId>();
    }

    public UpdateStatus Consolidated(
        Operation<FileSystemNodeModel<TId>> operation,
        UpdateTreeNodeModel<TId>? nodeModel,
        SyncedTreeNode<TId>? syncedNode)
    {
        if (operation.Type != OperationType.Create && nodeModel == null && syncedNode == null)
        {
            throw new InvalidOperationException($"Unable to consolidate {operation.Type} operation. Node with Id={operation.Model.Id} exists neither in Synced Tree neither in Update Tree!");
        }

        if (operation.Type != OperationType.Delete && nodeModel?.Status.Contains(UpdateStatus.Deleted) == true)
        {
            throw new InvalidOperationException($"Unable to consolidate {operation.Type} operation. Node with Id={operation.Model.Id} is already deleted!");
        }

        switch (operation.Type)
        {
            case OperationType.Create when syncedNode != null:
                throw new InvalidOperationException($"Unable to consolidate Create operation. Synced Tree node with Id={syncedNode.Id} already exists!");

            case OperationType.Create:
                return UpdateStatus.Created;

            case OperationType.Edit when syncedNode == null:
                return UpdateStatus.Created;

            case OperationType.Edit when nodeModel == null:
                return Edited(UpdateStatus.Unchanged, operation.Model, syncedNode);

            case OperationType.Edit:
                return Edited(nodeModel.Status, operation.Model, syncedNode);

            case OperationType.Move when syncedNode == null:
                return UpdateStatus.Created;

            case OperationType.Move when nodeModel == null:
                return RenamedAndOrMoved(UpdateStatus.Unchanged, operation.Model, syncedNode);

            case OperationType.Move:
                return RenamedAndOrMoved(nodeModel.Status, operation.Model, syncedNode);

            case OperationType.Delete:
                // Restore flag is preserved
                return nodeModel?.Status.Contains(UpdateStatus.Restore) == true
                    ? UpdateStatus.Deleted | UpdateStatus.Restore
                    : UpdateStatus.Deleted;

            default:
                throw new InvalidOperationException();
        }
    }

    public UpdateStatus Edited(
        UpdateStatus status,
        FileSystemNodeModel<TId> nodeModel,
        SyncedTreeNode<TId> syncedNode)
    {
        // Node is considered edited if its size and last write time in the Update Tree differs from
        // corresponding values in the Synced Tree. If one of the last write time values equals to
        // minimum value, it is not used in the comparison.
        status = _contentEqualityComparer.Equals(nodeModel, syncedNode.Model)
            ? status.Minus(UpdateStatus.Edited)
            : status.Union(UpdateStatus.Edited);

        return status;
    }

    public UpdateStatus RenamedAndOrMoved(
        UpdateStatus status,
        FileSystemNodeModel<TId> nodeModel,
        SyncedTreeNode<TId> syncedNode)
    {
        // Node is considered renamed if its name in the Update Tree differs from its name in the Synced Tree
        status = nodeModel.Name == syncedNode.Name
            ? status.Minus(UpdateStatus.Renamed)
            : status.Union(UpdateStatus.Renamed);

        // Node is considered moved if its parent in the Update Tree differs from its parent in the Synced Tree
        status = nodeModel.ParentId.Equals(syncedNode.Parent!.Model.OwnId(_replica))
            ? status.Minus(UpdateStatus.Moved)
            : status.Union(UpdateStatus.Moved);

        return status;
    }
}
