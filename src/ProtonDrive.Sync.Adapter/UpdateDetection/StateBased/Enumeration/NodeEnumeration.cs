using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Adapter.Shared;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.UpdateDetection.StateBased.Enumeration;

internal class NodeEnumeration<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly IScheduler _syncScheduler;
    private readonly FileSystemEnumeration<TId, TAltId> _fileSystemEnumeration;
    private readonly NodeEnumerationSuccessStep<TId, TAltId> _success;
    private readonly EnumerationFailureStep<TId, TAltId> _failure;
    private readonly ILogger<NodeEnumeration<TId, TAltId>> _logger;

    public NodeEnumeration(
        IScheduler syncScheduler,
        FileSystemEnumeration<TId, TAltId> fileSystemEnumeration,
        NodeEnumerationSuccessStep<TId, TAltId> success,
        EnumerationFailureStep<TId, TAltId> failure,
        ILogger<NodeEnumeration<TId, TAltId>> logger)
    {
        _syncScheduler = syncScheduler;
        _fileSystemEnumeration = fileSystemEnumeration;
        _success = success;
        _failure = failure;
        _logger = logger;
    }

    public async Task<Result<NodeInfo<TAltId>>> ExecuteAsync(AdapterTreeNode<TId, TAltId> node, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var nodeInfo = await Schedule(() => ShouldEnumerateNode(node) ? ToNodeInfo(node) : null).ConfigureAwait(false);

        if (nodeInfo == null)
        {
            // Nothing to enumerate
            return Result.Success<NodeInfo<TAltId>>();
        }

        try
        {
            var result = await _fileSystemEnumeration.EnumerateNode(nodeInfo, cancellationToken).ConfigureAwait(false);

            await Schedule(() => HandleSuccess(node, result)).ConfigureAwait(false);

            return Result.Success(nodeInfo);
        }
        catch (FileSystemClientException<TAltId> ex)
        {
            LogFailure(node, nodeInfo, ex);

            await Schedule(() => HandleFailure(ex, node)).ConfigureAwait(false);

            return Result.Failure(nodeInfo, ex);
        }
    }

    private bool ShouldEnumerateNode(AdapterTreeNode<TId, TAltId> node)
    {
        EscapeIfUnstable(node);

        return !node.IsRoot && node.Model.Status.HasAnyFlag(
            AdapterNodeStatus.DirtyPlaceholder |
            AdapterNodeStatus.DirtyAttributes);
    }

    private void HandleSuccess(
        AdapterTreeNode<TId, TAltId> currentNode,
        NodeInfo<TAltId> nodeInfo)
    {
        _success.Execute(currentNode, nodeInfo);
    }

    private void HandleFailure(Exception exception, AdapterTreeNode<TId, TAltId> node)
    {
        _failure.Execute(exception, node);
    }

    private void EscapeIfUnstable(AdapterTreeNode<TId, TAltId> node)
    {
        if (!IsNodeAndBranchStable(node))
        {
            throw new EscapeException();
        }
    }

    private bool IsNodeAndBranchStable(AdapterTreeNode<TId, TAltId> node)
    {
        if (node.IsDeleted)
        {
            return false;
        }

        return node.IsRoot ||
               (IsNodeStable(node) &&
                node.FromParentToRoot().All(StartsStableBranch));
    }

    private bool IsNodeStable(AdapterTreeNode<TId, TAltId> node)
    {
        var dirtyFlags = node.Model.Status & AdapterNodeStatus.DirtyNodeMask;

        // Deleted nodes are not enumerated
        if (dirtyFlags.HasFlag(AdapterNodeStatus.DirtyDeleted))
        {
            return false;
        }

        // Nodes, having both DirtyParent and DirtyAttributes flags set,
        // are candidates for node enumeration, but not for children enumeration
        if (dirtyFlags.HasFlag(AdapterNodeStatus.DirtyParent | AdapterNodeStatus.DirtyAttributes))
        {
            return true;
        }

        // Lost nodes (having DirtyParent flag set) are not enumerated
        return !dirtyFlags.HasFlag(AdapterNodeStatus.DirtyParent);
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

    private void LogFailure(AdapterTreeNode<TId, TAltId> node, NodeInfo<TAltId> nodeInfo, Exception exception)
    {
        _logger.LogWarning(
            "Enumerating {Type} \"{Root}\"/{Id} at parent with Id={ParentId} {ParentExternalId} failed: {ErrorMessage}",
            nodeInfo.IsDirectory() ? NodeType.Directory : NodeType.File,
            nodeInfo.Root?.Id,
            node.Id,
            node.Model.ParentId,
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
