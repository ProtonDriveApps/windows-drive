using System;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared.Trees.Changes;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Engine.Shared.Trees.Changes;

internal static class DetectedTreeChangesExtensions
{
    public static void DetectLocalChangesOf<TId>(this IDetectedTreeChanges<TId> detectedTreeChanges, SyncedTree<TId> syncedTree, UpdateTree<TId> updateTree)
        where TId : IEquatable<TId>
    {
        syncedTree.Operations.Executed += (_, args) => detectedTreeChanges.Add(ToOperation(args));

        updateTree.Operations.Executed += (_, args) =>
        {
            if (HasBecomeSynced(args))
            {
                var syncedNode = syncedTree.NodeByIdOrDefault((args.NewModel ?? args.OldModel)!.Id);
                if (syncedNode != null)
                {
                    detectedTreeChanges.Add(ToOperation(syncedNode));
                }
            }
        };
    }

    private static bool HasBecomeSynced<TId>(FileSystemTreeOperationExecutedEventArgs<UpdateTreeNodeModel<TId>, TId> eventArgs)
        where TId : IEquatable<TId>
    {
        // The Update Tree contains nodes that have diverged from the synced state in Synced Tree and ancestors connecting
        // diverged nodes with the root. Diverged nodes have Update Status value different than Unchanged.
        return (eventArgs.Type is OperationType.Delete && eventArgs.OldModel!.Status is not UpdateStatus.Unchanged) ||
                (eventArgs.Type is OperationType.Edit or OperationType.Move or OperationType.Update &&
                 eventArgs.OldModel!.Status is not UpdateStatus.Unchanged &&
                 eventArgs.NewModel!.Status is UpdateStatus.Unchanged);
    }

    private static Operation<FileSystemNodeModel<TId>> ToOperation<TId>(FileSystemTreeOperationExecutedEventArgs<SyncedTreeNodeModel<TId>, TId> eventArgs)
        where TId : IEquatable<TId>
    {
        return new Operation<FileSystemNodeModel<TId>>(
            eventArgs.Type,
            new FileSystemNodeModel<TId>().CopiedFrom((eventArgs.NewModel ?? eventArgs.OldModel)!));
    }

    private static Operation<FileSystemNodeModel<TId>> ToOperation<TId>(SyncedTreeNode<TId> syncedNode)
        where TId : IEquatable<TId>
    {
        return new Operation<FileSystemNodeModel<TId>>(
            OperationType.Update,
            new FileSystemNodeModel<TId>().CopiedFrom(syncedNode.Model));
    }
}
