using Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Propagation;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Engine.Propagation;

internal sealed class UnchangedLeafsDeletionPipeline<TId>
    where TId : IEquatable<TId>
{
    private readonly PropagationTree<TId> _propagationTree;
    private readonly IScheduler _syncScheduler;

    private readonly UnchangedLeafDeletionOperationsFactory<TId> _unchangedLeafDeletionOperationsFactory = new();

    public UnchangedLeafsDeletionPipeline(
        PropagationTree<TId> propagationTree,
        IScheduler syncScheduler)
    {
        _propagationTree = propagationTree;
        _syncScheduler = syncScheduler;
    }

    public Task ExecuteAsync(PropagationTreeNode<TId> node)
    {
        return Schedule(() => DeleteUnchangedLeafs(node));
    }

    private void DeleteUnchangedLeafs(PropagationTreeNode<TId> propagationNode)
    {
        foreach (var node in FromNodeToRoot(propagationNode).TakeWhile(n => n.IsLeaf))
        {
            _propagationTree.Operations.Execute(_unchangedLeafDeletionOperationsFactory.Operations(node));
        }
    }

    private IEnumerable<PropagationTreeNode<TId>> FromNodeToRoot(PropagationTreeNode<TId> node)
    {
        var parent = node.Parent;
        yield return node;

        while (!node.IsRoot)
        {
            node = parent!;
            parent = node.Parent;
            yield return node;
        }
    }

    private Task Schedule(Action origin) => _syncScheduler.Schedule(origin);
}
