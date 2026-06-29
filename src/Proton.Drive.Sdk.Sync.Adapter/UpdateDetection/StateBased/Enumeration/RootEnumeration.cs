using Proton.Drive.Sdk.Sync.Adapter.Trees.Adapter;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Adapter.UpdateDetection.StateBased.Enumeration;

internal sealed class RootEnumeration<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ITransactedScheduler _syncScheduler;
    private readonly AdapterTree<TId, TAltId> _adapterTree;
    private readonly FileSystemEnumeration<TId, TAltId> _fileSystemEnumeration;
    private readonly RootEnumerationSuccessStep<TId, TAltId> _success;
    private readonly RootEnumerationCompletionStep<TId, TAltId> _completion;

    public RootEnumeration(
        ITransactedScheduler syncScheduler,
        AdapterTree<TId, TAltId> adapterTree,
        FileSystemEnumeration<TId, TAltId> fileSystemEnumeration,
        RootEnumerationSuccessStep<TId, TAltId> success,
        RootEnumerationCompletionStep<TId, TAltId> completion)
    {
        _syncScheduler = syncScheduler;
        _adapterTree = adapterTree;
        _fileSystemEnumeration = fileSystemEnumeration;
        _success = success;
        _completion = completion;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var unprocessedSyncRootNodes = await Schedule(Prepare).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        await foreach (var rootInfo in EnumerateRoots(cancellationToken).ConfigureAwait(false))
        {
            await Schedule(() => HandleSuccess(rootInfo, unprocessedSyncRootNodes)).ConfigureAwait(false);
        }

        await ScheduleAndCommit(() => Complete(unprocessedSyncRootNodes)).ConfigureAwait(false);
    }

    private static RootInfo<TAltId> ToRootInfo(NodeInfo<TAltId> nodeInfo)
    {
        Ensure.NotNull(nodeInfo.Root, nameof(nodeInfo), nameof(nodeInfo.Root));

        if (nodeInfo.Root.Id == 0)
        {
            throw new ArgumentException("Root Id cannot have default value");
        }

        if (nodeInfo.Root.NodeId.Equals(default))
        {
            throw new ArgumentException("Root NodeId cannot have default value");
        }

        if (!nodeInfo.Root.NodeId.Equals(nodeInfo.Id))
        {
            throw new ArgumentException("NodeInfo Id value must be equal to Root NodeId value");
        }

        if (!nodeInfo.Name.Equals(nodeInfo.Root.Id.ToString()))
        {
            throw new ArgumentException("NodeInfo Name value must be equal to Root Id value");
        }

        return nodeInfo.Root;
    }

    private IDictionary<TId, AdapterTreeNode<TId, TAltId>> Prepare()
    {
        var rootNode = _adapterTree.Root;

        return rootNode.Children.ToDictionary(n => n.Id);
    }

    private IAsyncEnumerable<RootInfo<TAltId>> EnumerateRoots(CancellationToken cancellationToken)
    {
        return _fileSystemEnumeration.EnumerateChildren(NodeInfo<TAltId>.Directory(), cancellationToken).Select(ToRootInfo);
    }

    private void HandleSuccess(
        RootInfo<TAltId> rootInfo,
        IDictionary<TId, AdapterTreeNode<TId, TAltId>> unprocessedSyncRootNodes)
    {
        _success.Execute(rootInfo, unprocessedSyncRootNodes);
    }

    private void Complete(
        IDictionary<TId, AdapterTreeNode<TId, TAltId>> unprocessedChildren)
    {
        var rootNode = _adapterTree.Root;

        _completion.Execute(rootNode, unprocessedChildren);
    }

    private Task<T> Schedule<T>(Func<T> origin)
    {
        return _syncScheduler.Schedule(origin);
    }

    private Task Schedule(Action origin)
    {
        return _syncScheduler.Schedule(origin);
    }

    private Task ScheduleAndCommit(Action origin)
    {
        return _syncScheduler.ScheduleAndCommit(origin);
    }
}
