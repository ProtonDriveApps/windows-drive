using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Adapter.NodeCopying;
using ProtonDrive.Sync.Adapter.Shared;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.UpdateDetection.StateBased.Enumeration;

internal class NodeEnumerationSuccessStep<TId, TAltId> : SuccessStep<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    public NodeEnumerationSuccessStep(
        ILogger<NodeEnumerationSuccessStep<TId, TAltId>> logger,
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
        AdapterTreeNode<TId, TAltId> currentNode,
        NodeInfo<TAltId> nodeInfo)
    {
        Ensure.IsFalse(currentNode.IsRoot, "Tree root node cannot be enumerated", nameof(currentNode));

        EscapeIfDeleted(currentNode);

        if (currentNode.IsSyncRoot())
        {
            // Changes to the sync root are ignored
            var currentNodeModel = IncomingAdapterTreeNodeModel<TId, TAltId>.FromNodeModel(currentNode.Model);

            ValidateAndUpdate(currentNode, currentNodeModel, currentNode.Parent);

            return;
        }

        var incomingNodeModel = new IncomingAdapterTreeNodeModel<TId, TAltId>
        {
            Id = currentNode.Model.Id,
            Type = nodeInfo.IsDirectory() ? NodeType.Directory : NodeType.File,
            ParentId = currentNode.Model.ParentId,
            Name = nodeInfo.Name,
            RevisionId = nodeInfo.RevisionId,
            LastWriteTime = nodeInfo.LastWriteTimeUtc,
            Size = nodeInfo.Size >= 0 ? nodeInfo.Size : nodeInfo.SizeOnStorage ?? 0L,
            SizeOnStorage = nodeInfo.SizeOnStorage,
            AltId = nodeInfo.GetCompoundId(),
            Status = currentNode.Model.Status,
        };

        var isDirtyPlaceholder = currentNode.Model.IsDirtyPlaceholder();

        if (isDirtyPlaceholder)
        {
            var node = ExistingNode(incomingNodeModel);
            var parentNode = currentNode.Parent;

            // Already existing node
            if (node != null)
            {
                // Removing the dirty placeholder
                DetectNodeUpdate(currentNode, null);

                if (ShouldBeIgnored(node, incomingNodeModel.AltId, nodeInfo.Name, nodeInfo.Attributes, nodeInfo.PlaceholderState, parentNode))
                {
                    MarkAsDeleted(node);

                    return;
                }

                // Updating the existing node
                incomingNodeModel = incomingNodeModel
                    .WithStatus(node.Model.Status)
                    .WithStateUpdateFlags(GetStateUpdateFlags(parentNode, nodeInfo));

                ValidateAndUpdate(node, incomingNodeModel, parentNode);
            }
            else
            {
                // A new node
                if (ShouldBeIgnored(node, incomingNodeModel.AltId, nodeInfo.Name, nodeInfo.Attributes, nodeInfo.PlaceholderState, parentNode))
                {
                    // Removing the dirty placeholder
                    DetectNodeUpdate(currentNode, null);

                    return;
                }

                if (incomingNodeModel.Type == currentNode.Model.Type)
                {
                    // Node type matches.
                    // Should be a directory as dirty placeholders are created as directories.
                    incomingNodeModel = incomingNodeModel
                        .WithDirtyFlags(AdapterNodeStatus.DirtyDescendants)
                        .WithStateUpdateFlags(GetStateUpdateFlags(parentNode, nodeInfo));

                    // Updating dirty placeholder
                    ValidateAndUpdate(currentNode, incomingNodeModel, parentNode);
                }
                else
                {
                    // Node type doesn't mach
                    // Should be a file as dirty placeholders are created as directories
                    // Removing the dirty placeholder
                    DetectNodeUpdate(currentNode, null);

                    incomingNodeModel = incomingNodeModel
                        .WithStateUpdateFlags(GetStateUpdateFlags(parentNode, nodeInfo));

                    // Creating a new node
                    ValidateAndUpdate(null, incomingNodeModel, parentNode);
                }
            }
        }
        else
        {
            var parentNode = !IsDefault(nodeInfo.ParentId) ? ParentNode(nodeInfo.GetCompoundParentId()) : currentNode.Parent;

            if (ShouldBeIgnored(currentNode, incomingNodeModel.AltId, nodeInfo.Name, nodeInfo.Attributes, nodeInfo.PlaceholderState, parentNode))
            {
                MarkAsDeleted(currentNode);

                return;
            }

            incomingNodeModel = incomingNodeModel.Copy()
                .WithParentId(parentNode.Id)
                .WithStateUpdateFlags(GetStateUpdateFlags(parentNode, nodeInfo));

            // Updating the current node
            ValidateAndUpdate(currentNode, incomingNodeModel, parentNode);
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
