using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared;
using ProtonDrive.Sync.Adapter.NodeCopying;
using ProtonDrive.Sync.Adapter.Shared;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.UpdateDetection.StateBased.Enumeration;

internal class ChildrenEnumerationSuccessStep<TId, TAltId> : SuccessStep<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    public ChildrenEnumerationSuccessStep(
        ILogger<ChildrenEnumerationSuccessStep<TId, TAltId>> logger,
        AdapterTree<TId, TAltId> adapterTree,
        IDirtyNodes<TId, TAltId> dirtyNodes,
        IIdentitySource<TId> idSource,
        IReadOnlyDictionary<TId, RootInfo<TAltId>> syncRoots,
        ICopiedNodes<TId, TAltId> copiedNodes,
        NodeUpdateDetection<TId, TAltId> nodeUpdateDetection,
        IItemExclusionFilter itemExclusionFilter)
        : base(logger, adapterTree, dirtyNodes, idSource, nodeUpdateDetection, syncRoots, copiedNodes, itemExclusionFilter)
    {
    }

    public void Execute(
        AdapterTreeNode<TId, TAltId> parentNode,
        NodeInfo<TAltId> nodeInfo,
        IDictionary<TId, AdapterTreeNode<TId, TAltId>> unprocessedChildren)
    {
        Ensure.NotNullOrEmpty(nodeInfo.Name, nameof(nodeInfo), nameof(nodeInfo.Name));

        if (parentNode.IsRoot && !nodeInfo.IsDirectory())
        {
            throw new InvalidOperationException("First level node must be a folder (sync root)");
        }

        EscapeIfDeleted(parentNode);

        var incomingNodeModel = new IncomingAdapterTreeNodeModel<TId, TAltId>
        {
            Type = nodeInfo.IsDirectory() ? NodeType.Directory : NodeType.File,
            Name = nodeInfo.Name,
            RevisionId = nodeInfo.RevisionId,
            LastWriteTime = nodeInfo.LastWriteTimeUtc,
            Size = nodeInfo.Size >= 0 ? nodeInfo.Size : nodeInfo.SizeOnStorage ?? 0L,
            SizeOnStorage = nodeInfo.SizeOnStorage,
            AltId = nodeInfo.GetCompoundId(),
            ParentId = parentNode.Id,
        };

        var existingNode = ExistingNode(incomingNodeModel);

        if (existingNode != null)
        {
            // Existing node
            unprocessedChildren.Remove(existingNode.Id);

            if (existingNode.IsSyncRoot())
            {
                // Changes to the sync root are ignored
                return;
            }
        }

        if (ShouldBeIgnored(existingNode, incomingNodeModel.AltId, nodeInfo.Name, nodeInfo.Attributes, nodeInfo.PlaceholderState, parentNode))
        {
            MarkAsDeleted(existingNode);

            return;
        }

        if (existingNode != null)
        {
            incomingNodeModel = incomingNodeModel
                .WithId(existingNode.Model.Id)
                .WithStatus(existingNode.Model.Status)
                .WithStateUpdateFlags(GetStateUpdateFlags(parentNode, nodeInfo));

            ValidateAndUpdate(existingNode, incomingNodeModel, parentNode);
        }
        else
        {
            incomingNodeModel = incomingNodeModel
                .WithStateUpdateFlags(GetStateUpdateFlags(parentNode, nodeInfo));

            // A new node
            ValidateAndUpdate(null, incomingNodeModel, parentNode);
        }
    }

    private AdapterNodeStatus GetStateUpdateFlags(AdapterTreeNode<TId, TAltId> parentNode, NodeInfo<TAltId> nodeInfo)
    {
        return nodeInfo.PlaceholderState.GetStateUpdateFlags(nodeInfo.Attributes, GetRoot(parentNode));
    }

    private void EscapeIfDeleted(AdapterTreeNode<TId, TAltId> node)
    {
        if (node.IsDeleted)
        {
            Escape();
        }
    }

    private void Escape()
    {
        throw new EscapeException();
    }
}
