using System;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Adapters;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Engine.Propagation;

internal class NameConflictResolutionPipeline<TId>
    where TId : struct, IEquatable<TId>
{
    private readonly Replica _replica;
    private readonly IScheduler _syncScheduler;
    private readonly ISyncAdapter<TId> _adapter;
    private readonly SyncedTree<TId> _syncedTree;
    private readonly UpdateTree<TId> _ownUpdateTree;
    private readonly PropagationTree<TId> _propagationTree;
    private readonly IFileNameFactory<TId> _uniqueNameFactory;
    private readonly PropagatingNodes<TId> _propagatingNodes;

    public NameConflictResolutionPipeline(
        Replica replica,
        IScheduler syncScheduler,
        ISyncAdapter<TId> adapter,
        SyncedTree<TId> syncedTree,
        UpdateTree<TId> ownUpdateTree,
        PropagationTree<TId> propagationTree,
        IFileNameFactory<TId> uniqueNameFactory,
        PropagatingNodes<TId> propagatingNodes)
    {
        _replica = replica;
        _syncScheduler = syncScheduler;
        _adapter = adapter;
        _syncedTree = syncedTree;
        _ownUpdateTree = ownUpdateTree;
        _propagationTree = propagationTree;
        _uniqueNameFactory = uniqueNameFactory;
        _propagatingNodes = propagatingNodes;
    }

    /// <summary>
    /// Resolves a name conflict detected by the File System Adapter during operation execution.
    /// Renames the conflicting node to a temporary name if possible.
    /// </summary>
    /// <remarks>
    /// The conflict is resolved by temporary renaming the conflicting node.
    /// </remarks>
    /// <param name="id">Identity value of the conflicting node.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>True if conflicting node is successfully renamed; False otherwise.</returns>
    public async Task<bool> ExecuteAsync(TId? id, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            return false;
        }

        // The node is "locked" to prevent parallel propagation of changes
        // (operation execution) to the same node.
        // If the node is already locked, name conflict resolution is skipped,
        // therefore, execution of the operation that faced this name conflict
        // will also be skipped.
        var (success, result) = await _propagatingNodes.LockNodeAndExecute(
                id.Value,
                () => InternalExecuteAsync(id.Value, cancellationToken))
            .ConfigureAwait(false);

        return success && result;
    }

    public async Task<bool> InternalExecuteAsync(TId id, CancellationToken cancellationToken)
    {
        var operation = await Schedule(() => CreateTemporaryRenameOperation(id, cancellationToken)).ConfigureAwait(false);

        if (operation == null)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();

        return (await _adapter.ExecuteOperation(operation, cancellationToken).ConfigureAwait(false)).Succeeded();
    }

    private ExecutableOperation<TId>? CreateTemporaryRenameOperation(TId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var propagationNode = _propagationTree.NodeByOwnIdOrDefault(id, _replica);
        if (propagationNode == null)
        {
            // The conflicting node cannot be altered
            return null;
        }

        var status = OwnStatus(propagationNode.Model);
        if (!status.Contains(UpdateStatus.Deleted) &&
            !status.Contains(UpdateStatus.Renamed) &&
            !status.Contains(UpdateStatus.Moved))
        {
            // The conflicting node cannot be altered
            return null;
        }

        var originalNodeModel = (IFileSystemNodeModel<TId>?)_ownUpdateTree.NodeByIdOrDefault(propagationNode.Model.OwnId(_replica))?.Model
                                ?? ToOwnNodeModel(_syncedTree.NodeByIdOrDefault(propagationNode.Id))
                                ?? throw new InvalidOperationException();

        // AltId is not used
        var model = new AltIdentifiableFileSystemNodeModel<TId, TId>()
            .CopiedFrom(originalNodeModel)
            .WithName<AltIdentifiableFileSystemNodeModel<TId, TId>, TId>(NameCandidate(propagationNode.Model));

        return new ExecutableOperation<TId>(OperationType.Move, model, backup: false);
    }

    private string NameCandidate(IFileSystemNodeModel<TId> nodeModel)
    {
        return _uniqueNameFactory.GetName(nodeModel);
    }

    private UpdateStatus OwnStatus(PropagationTreeNodeModel<TId> model)
    {
        return _replica == Replica.Remote ? model.RemoteStatus : model.LocalStatus;
    }

    private FileSystemNodeModel<TId>? ToOwnNodeModel(SyncedTreeNode<TId>? node)
    {
        if (node == null)
        {
            return null;
        }

        return _replica == Replica.Remote
            ? ToRemote<SyncedTree<TId>, SyncedTreeNode<TId>, SyncedTreeNodeModel<TId>>(node)
            : ToLocal<SyncedTree<TId>, SyncedTreeNode<TId>, SyncedTreeNodeModel<TId>>(node);
    }

    private FileSystemNodeModel<TId> ToLocal<TTree, TNode, TModel>(TNode node)
        where TTree : FileSystemTree<TTree, TNode, TModel, TId>
        where TNode : FileSystemNode<TTree, TNode, TModel, TId>
        where TModel : AltIdentifiableFileSystemNodeModel<TId, TId>, new()
    {
        return new FileSystemNodeModel<TId>()
            .CopiedFrom(node.Model);
    }

    private FileSystemNodeModel<TId> ToRemote<TTree, TNode, TModel>(TNode node)
        where TTree : FileSystemTree<TTree, TNode, TModel, TId>
        where TNode : FileSystemNode<TTree, TNode, TModel, TId>
        where TModel : AltIdentifiableFileSystemNodeModel<TId, TId>, new()
    {
        return new FileSystemNodeModel<TId>()
            .CopiedFrom(node.Model)
            .WithId(node.Model.AltId)
            .WithParentId(node.Parent!.Model.AltId);
    }

    private Task<T> Schedule<T>(Func<T> origin)
    {
        return _syncScheduler.Schedule(origin);
    }
}
