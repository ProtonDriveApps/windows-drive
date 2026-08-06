using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Adapter.Shared;
using Proton.Drive.Sdk.Sync.Adapter.Trees.Adapter;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Adapter.UpdateDetection.StateBased.Enumeration;

internal class ChildrenEnumeration<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly IScheduler _syncScheduler;
    private readonly FileSystemEnumeration<TId, TAltId> _fileSystemEnumeration;
    private readonly ChildrenEnumerationPreparationStep<TId, TAltId> _preparation;
    private readonly ChildrenEnumerationSuccessStep<TId, TAltId> _success;
    private readonly EnumerationFailureStep<TId, TAltId> _failure;
    private readonly ChildrenEnumerationCompletionStep<TId, TAltId> _completion;
    private readonly ILogger<ChildrenEnumeration<TId, TAltId>> _logger;

    public ChildrenEnumeration(
        IScheduler syncScheduler,
        FileSystemEnumeration<TId, TAltId> fileSystemEnumeration,
        ChildrenEnumerationPreparationStep<TId, TAltId> preparation,
        ChildrenEnumerationSuccessStep<TId, TAltId> success,
        EnumerationFailureStep<TId, TAltId> failure,
        ChildrenEnumerationCompletionStep<TId, TAltId> completion,
        ILogger<ChildrenEnumeration<TId, TAltId>> logger)
    {
        _syncScheduler = syncScheduler;
        _fileSystemEnumeration = fileSystemEnumeration;
        _preparation = preparation;
        _success = success;
        _failure = failure;
        _completion = completion;
        _logger = logger;
    }

    public async Task<Result<NodeInfo<TAltId>>> ExecuteAsync(AdapterTreeNode<TId, TAltId> node, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (nodeModel, nodeInfo, unprocessedChildren) = await Schedule(() =>
                ShouldEnumerateChildren(node)
                    ? (node.Model, ToNodeInfo(node), Prepare(node))
                    : (null, null, null))
            .ConfigureAwait(false);

        if (nodeModel is null || nodeInfo is null || unprocessedChildren is null)
        {
            // Nothing to enumerate
            return Result.Success<NodeInfo<TAltId>>();
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await foreach (var child in EnumerateChildren(nodeInfo, cancellationToken).ConfigureAwait(false))
            {
                await Schedule(() => HandleSuccess(node, child, unprocessedChildren)).ConfigureAwait(false);
            }

            await Schedule(() => Complete(node, unprocessedChildren)).ConfigureAwait(false);

            return Result.Success(nodeInfo);
        }
        catch (FileSystemClientException<TAltId> ex)
        {
            LogFailure(nodeModel, nodeInfo, ex);

            await Schedule(() => HandleFailure(ex, nodeModel)).ConfigureAwait(false);

            return Result.Failure(nodeInfo, ex);
        }
    }

    private bool ShouldEnumerateChildren(AdapterTreeNode<TId, TAltId> node)
    {
        EscapeIfUnstable(node);

        return node.Model.Status.HasFlag(AdapterNodeStatus.DirtyChildren);
    }

    private IDictionary<TId, AdapterTreeNode<TId, TAltId>> Prepare(AdapterTreeNode<TId, TAltId> node)
    {
        return _preparation.Execute(node);
    }

    private void HandleSuccess(
        AdapterTreeNode<TId, TAltId> parentNode,
        NodeInfo<TAltId> nodeInfo,
        IDictionary<TId, AdapterTreeNode<TId, TAltId>> unprocessedChildren)
    {
        _success.Execute(parentNode, nodeInfo, unprocessedChildren);
    }

    private void HandleFailure(Exception exception, AdapterTreeNodeModel<TId, TAltId> nodeModel)
    {
        _failure.Execute(exception, nodeModel);
    }

    private void Complete(
        AdapterTreeNode<TId, TAltId> node,
        IDictionary<TId, AdapterTreeNode<TId, TAltId>> unprocessedChildren)
    {
        _completion.Execute(node, unprocessedChildren);
    }

    private void EscapeIfUnstable(AdapterTreeNode<TId, TAltId> node)
    {
        if (!IsNodeAndBranchStable(node))
        {
            Escape();
        }
    }

    private void Escape()
    {
        throw new EscapeException();
    }

    private bool IsNodeAndBranchStable(AdapterTreeNode<TId, TAltId> node)
    {
        if (node.IsDeleted)
        {
            return false;
        }

        return node.IsRoot ||
               (!node.Model.IsLostOrDeleted() &&
                node.FromParentToRoot().All(StartsStableBranch));
    }

    private bool StartsStableBranch(AdapterTreeNode<TId, TAltId> node)
    {
        return !node.Model.HasDirtyDescendantsFlag() &&
               !node.Model.IsLostOrDeleted() &&
               !node.Model.IsDirtyPlaceholder();
    }

    private NodeInfo<TAltId> ToNodeInfo(AdapterTreeNode<TId, TAltId> node)
    {
        return _fileSystemEnumeration.ToNodeInfo(node);
    }

    private IAsyncEnumerable<NodeInfo<TAltId>> EnumerateChildren(NodeInfo<TAltId> nodeInfo, CancellationToken cancellationToken)
    {
        return _fileSystemEnumeration.EnumerateChildren(nodeInfo, cancellationToken);
    }

    private void LogFailure(AdapterTreeNodeModel<TId, TAltId> nodeModel, NodeInfo<TAltId> nodeInfo, Exception exception)
    {
        _logger.LogWarning(
            "Enumerating children of {Type} \"{Root}\"/{Id} at parent with Id={ParentId} {ParentExternalId} failed: {ErrorMessage}",
            nodeInfo.IsDirectory() ? NodeType.Directory : NodeType.File,
            nodeInfo.Root?.Id,
            nodeModel.Id,
            nodeModel.ParentId,
            nodeInfo.GetCompoundParentId(),
            exception.CombinedMessage());
    }

    private Task<T> Schedule<T>(Func<T> origin)
    {
        return _syncScheduler.Schedule(origin);
    }

    private Task Schedule(Action origin)
    {
        return _syncScheduler.Schedule(origin);
    }
}
