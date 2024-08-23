using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Adapter.Trees.Adapter.NodeLinking;
using ProtonDrive.Sync.Adapter.Trees.Dirty;
using ProtonDrive.Sync.Shared.Trees;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem.Traversal;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.NodeCopying;

internal sealed class CopiedNodesHandler<TId, TAltId> : ICopiedNodes<TId, TAltId>
    where TId : struct, IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<CopiedNodesHandler<TId, TAltId>> _logger;
    private readonly AdapterTree<TId, TAltId> _adapterTree;
    private readonly DirtyTree<TId> _dirtyTree;
    private readonly INodeLinkRepository<TId> _nodeLinkRepository;

    private readonly PassiveTreeTraversal<DirtyTree<TId>, DirtyTreeNode<TId>, DirtyTreeNodeModel<TId>, TId> _dirtyTreeTraversal;

    private bool _isDeleting;

    public CopiedNodesHandler(
        ILogger<CopiedNodesHandler<TId, TAltId>> logger,
        AdapterTree<TId, TAltId> adapterTree,
        DirtyTree<TId> dirtyTree,
        INodeLinkRepository<TId> nodeLinkRepository)
    {
        _logger = logger;
        _adapterTree = adapterTree;
        _dirtyTree = dirtyTree;
        _nodeLinkRepository = nodeLinkRepository;

        _dirtyTreeTraversal = new PassiveTreeTraversal<DirtyTree<TId>, DirtyTreeNode<TId>, DirtyTreeNodeModel<TId>, TId>();

        _adapterTree.Operations.Executing += OnAdapterTreeOperationExecuting;
    }

    public AdapterTreeNode<TId, TAltId>? GetSourceNodeOrDefault(TId destinationNodeId)
    {
        var node = _dirtyTree.NodeByIdOrDefault(destinationNodeId);

        if (node is null || !node.Model.Status.HasFlag(AdapterNodeStatus.DirtyCopiedTo))
        {
            return default;
        }

        return GetSourceNode(destinationNodeId);
    }

    public void Add(AdapterTreeNode<TId, TAltId> sourceNode, AdapterTreeNode<TId, TAltId> destinationNode)
    {
        if (destinationNode.Model.Status.HasFlag(AdapterNodeStatus.Synced))
        {
            throw new TreeException($"Adapter Tree node with ID={destinationNode.Id} has Synced flag set and cannot be destination in a Copy link");
        }

        if (sourceNode.Model.Status.HasFlag(AdapterNodeStatus.DirtyCopiedTo))
        {
            sourceNode = GetSourceNode(sourceNode.Id);
            RemoveLinkToDestination(sourceNode.Model, removeStatusFlag: false);
        }

        _nodeLinkRepository.Add(NodeLinkType.Copied, sourceNode.Id, destinationNode.Id);

        AppendSourceStatusFlag(sourceNode.Model);
        AppendDestinationStatusFlag(destinationNode.Model);
    }

    public void RemoveLinksInBranch(TId nodeId)
    {
        RemoveLinksInBranch(nodeId, isDeleting: false);
    }

    private static bool IsDefault([NotNullWhen(false)] TId? value)
    {
        return value is null || value.Equals(default(TId));
    }

    private void OnAdapterTreeOperationExecuting(object? sender, FileSystemTreeOperationExecutingEventArgs<AdapterTreeNodeModel<TId, TAltId>, TId> eventArgs)
    {
        switch (eventArgs.Type)
        {
            case OperationType.Update when
                eventArgs.OldModel!.Status.HasFlag(AdapterNodeStatus.DirtyCopiedTo) &&
                !eventArgs.OldModel!.Status.HasFlag(AdapterNodeStatus.Synced) &&
                eventArgs.NewModel!.Status.HasFlag(AdapterNodeStatus.DirtyCopiedTo | AdapterNodeStatus.Synced):

                HandleUpdatingToSynced(eventArgs);

                break;

            case OperationType.Delete:

                HandleDeleting(eventArgs);

                break;
        }
    }

    private void HandleUpdatingToSynced(FileSystemTreeOperationExecutingEventArgs<AdapterTreeNodeModel<TId, TAltId>, TId> eventArgs)
    {
        RemoveLinkToSource(eventArgs.NewModel!, removeStatusFlag: false);
        eventArgs.NewModel = eventArgs.NewModel!.Copy().WithRemovedFlags(AdapterNodeStatus.DirtyCopiedTo);
    }

    private void HandleDeleting(FileSystemTreeOperationExecutingEventArgs<AdapterTreeNodeModel<TId, TAltId>, TId> eventArgs)
    {
        if (_isDeleting)
        {
            return;
        }

        _isDeleting = true;
        try
        {
            RemoveLinksInBranch(eventArgs.OldModel!.Id, isDeleting: true);
        }
        finally
        {
            _isDeleting = false;
        }
    }

    private void RemoveLinksInBranch(TId nodeId, bool isDeleting)
    {
        var startingNode = _dirtyTree.NodeByIdOrDefault(nodeId);
        if (startingNode == null)
        {
            return;
        }

        var copiedNodes =
            _dirtyTreeTraversal
                .IncludeStartingNode()
                .PreOrder(startingNode)
                .Where(n => n.Model.Status.HasAnyFlag(AdapterNodeStatus.DirtyCopiedFrom | AdapterNodeStatus.DirtyCopiedTo))
                .Select(AdapterTreeNode);

        foreach (var copiedNode in copiedNodes)
        {
            if (copiedNode.Model.Status.HasFlag(AdapterNodeStatus.DirtyCopiedFrom))
            {
                RemoveLinkToDestination(copiedNode.Model, removeStatusFlag: !isDeleting);
            }

            if (copiedNode.Model.Status.HasFlag(AdapterNodeStatus.DirtyCopiedTo))
            {
                RemoveLinkToSource(copiedNode.Model, removeStatusFlag: !isDeleting);
            }
        }
    }

    private void RemoveLinkToDestination(AdapterTreeNodeModel<TId, TAltId> sourceNodeModel, bool removeStatusFlag)
    {
        var destinationNodeModel = GetDestinationNode(sourceNodeModel.Id).Model;

        RemoveLink(sourceNodeModel);
        RemoveDestinationStatusFlag(destinationNodeModel);

        if (removeStatusFlag)
        {
            RemoveSourceStatusFlag(sourceNodeModel);
        }
    }

    private void RemoveLinkToSource(AdapterTreeNodeModel<TId, TAltId> destinationNodeModel, bool removeStatusFlag)
    {
        var sourceNodeModel = GetSourceNode(destinationNodeModel.Id).Model;

        RemoveLink(sourceNodeModel);
        RemoveSourceStatusFlag(sourceNodeModel);

        if (removeStatusFlag)
        {
            RemoveDestinationStatusFlag(destinationNodeModel);
        }
    }

    private void RemoveLink(AdapterTreeNodeModel<TId, TAltId> sourceNodeModel)
    {
        _nodeLinkRepository.Delete(NodeLinkType.Copied, sourceNodeModel.Id);
    }

    private void AppendSourceStatusFlag(AdapterTreeNodeModel<TId, TAltId> sourceNodeModel)
    {
        AppendStatusFlag(sourceNodeModel, AdapterNodeStatus.DirtyCopiedFrom);
    }

    private void AppendDestinationStatusFlag(AdapterTreeNodeModel<TId, TAltId> destinationNodeModel)
    {
        AppendStatusFlag(destinationNodeModel, AdapterNodeStatus.DirtyCopiedTo);
    }

    private void AppendStatusFlag(AdapterTreeNodeModel<TId, TAltId> nodeModel, AdapterNodeStatus statusFlag)
    {
        if (nodeModel.Status.HasFlag(statusFlag))
        {
            return;
        }

        _logger.LogDebug("Updating Adapter Tree node with Id={Id} to set status flag(s) ({Flags})", nodeModel.Id, statusFlag);

        _adapterTree.Operations.Execute(
            new Operation<AdapterTreeNodeModel<TId, TAltId>>(
                OperationType.Update,
                nodeModel.Copy().WithAppendedFlags(statusFlag)));
    }

    private void RemoveSourceStatusFlag(AdapterTreeNodeModel<TId, TAltId> sourceNodeModel)
    {
        RemoveStatusFlag(sourceNodeModel, AdapterNodeStatus.DirtyCopiedFrom);
    }

    private void RemoveDestinationStatusFlag(AdapterTreeNodeModel<TId, TAltId> destinationNodeModel)
    {
        RemoveStatusFlag(destinationNodeModel, AdapterNodeStatus.DirtyCopiedTo);
    }

    private void RemoveStatusFlag(AdapterTreeNodeModel<TId, TAltId> nodeModel, AdapterNodeStatus statusFlag)
    {
        if (!nodeModel.Status.HasFlag(statusFlag))
        {
            return;
        }

        _logger.LogDebug("Updating Adapter Tree node with Id={Id} to clear status flag(s) ({Flags})", nodeModel.Id, statusFlag);

        _adapterTree.Operations.Execute(
            new Operation<AdapterTreeNodeModel<TId, TAltId>>(
                OperationType.Update,
                nodeModel.Copy().WithRemovedFlags(statusFlag)));
    }

    private AdapterTreeNode<TId, TAltId> GetSourceNode(TId destinationNodeId)
    {
        var nodeId = _nodeLinkRepository.GetSourceNodeIdOrDefault(NodeLinkType.Copied, destinationNodeId);

        if (IsDefault(nodeId))
        {
            throw new TreeException($"Adapter Tree node with Id={destinationNodeId} has no link to the copy source node");
        }

        return _adapterTree.NodeById(nodeId);
    }

    private AdapterTreeNode<TId, TAltId> GetDestinationNode(TId sourceNodeId)
    {
        var nodeId = _nodeLinkRepository.GetDestinationNodeIdOrDefault(NodeLinkType.Copied, sourceNodeId);

        if (IsDefault(nodeId))
        {
            throw new TreeException($"Adapter Tree node with Id={sourceNodeId} has no link to the copy destination node");
        }

        return _adapterTree.FileById(nodeId);
    }

    private AdapterTreeNode<TId, TAltId> AdapterTreeNode(DirtyTreeNode<TId> node)
    {
        return _adapterTree.NodeByIdOrDefault(node.Id) ??
            throw new TreeException($"Adapter Tree node with Id={node.Id} does not exist");
    }
}
