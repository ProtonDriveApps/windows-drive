using System;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Adapter.UpdateDetection;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem.Traversal;

namespace ProtonDrive.Sync.Adapter.Shared;

internal abstract partial class SuccessStep<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly PassiveTreeTraversal<AdapterTree<TId, TAltId>, AdapterTreeNode<TId, TAltId>, AdapterTreeNodeModel<TId, TAltId>, TId>
        _adapterTreeTraversal = new();

    internal void CopyAndDelete(
        AdapterTreeNode<TId, TAltId> startingNode,
        AdapterTreeNodeModel<TId, TAltId> incomingNodeModel,
        RootInfo<TAltId> destinationRoot)
    {
        var destinationNode = CopyStartingNode(startingNode, incomingNodeModel, destinationRoot);

        var startingNodeIsDirty = BranchIsDirty(startingNode) || startingNode.IsNodeOrBranchDeleted();

        if (startingNodeIsDirty || startingNode.IsLeaf)
        {
            MarkAsDeleted(startingNode);

            return;
        }

        var nodeSkipped = false;

        _adapterTreeTraversal
            .ExcludeStartingNode()
            .DepthFirst(startingNode)
            .PreOrder(PreOrder)
            .PostOrder(PostOrder)
            .Execute();

        MarkAsDeleted(startingNode);

        return;

        void PreOrder(AdapterTreeNode<TId, TAltId> node)
        {
            if (node.Model.IsDirtyPlaceholder() || node.Model.IsLostOrDeleted())
            {
                _adapterTreeTraversal.SkipChildren();
                nodeSkipped = true;

                return;
            }

            destinationNode = CopyNode(node, destinationNode, destinationRoot);

            if (node.Model.HasDirtyDescendantsFlag())
            {
                _adapterTreeTraversal.SkipChildren();
            }
        }

        void PostOrder(AdapterTreeNode<TId, TAltId> node)
        {
            if (nodeSkipped)
            {
                nodeSkipped = false;

                return;
            }

            destinationNode = destinationNode.Parent!;
        }
    }

    private AdapterTreeNode<TId, TAltId> CopyStartingNode(
        AdapterTreeNode<TId, TAltId> node,
        AdapterTreeNodeModel<TId, TAltId> nodeModel,
        RootInfo<TAltId> root)
    {
        RemoveAltId(node);

        nodeModel = nodeModel.Copy().WithRemovedFlags(AdapterNodeStatus.DirtyMask | AdapterNodeStatus.Synced);

        return CreateNodeCopy(node, nodeModel, root);
    }

    private AdapterTreeNode<TId, TAltId> CopyNode(
        AdapterTreeNode<TId, TAltId> node,
        AdapterTreeNode<TId, TAltId> parentNode,
        RootInfo<TAltId> root)
    {
        var altId = node.Model.AltId;

        RemoveAltId(node);

        var nodeModel = node.Model.Copy().WithAltId(altId).WithParentId(parentNode.Id).WithRemovedFlags(AdapterNodeStatus.Synced);

        if (!root.IsOnDemand)
        {
            nodeModel = nodeModel.WithStateUpdateFlags(AdapterNodeStatus.None);
        }

        return CreateNodeCopy(node, nodeModel, root);
    }

    private AdapterTreeNode<TId, TAltId> CreateNodeCopy(
        AdapterTreeNode<TId, TAltId> node,
        AdapterTreeNodeModel<TId, TAltId> nodeModel,
        RootInfo<TAltId> root)
    {
        var destinationNode = CreateNode(nodeModel);

        if (root.IsOnDemand && node.Type is NodeType.File)
        {
            _copiedNodes.Add(node, destinationNode);
        }

        return destinationNode;
    }

    private AdapterTreeNode<TId, TAltId> CreateNode(AdapterTreeNodeModel<TId, TAltId> nodeModel)
    {
        var incomingNodeModel = IncomingAdapterTreeNodeModel<TId, TAltId>
            .FromNodeModel(nodeModel)
            .WithId(_idSource.NextValue());

        DetectNodeUpdate(null, incomingNodeModel);

        return _adapterTree.NodeById(incomingNodeModel.Id);
    }
}
