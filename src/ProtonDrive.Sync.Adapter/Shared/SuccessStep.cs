using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.IO;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Sync.Adapter.NodeCopying;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Adapter.UpdateDetection;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.Shared;

internal abstract partial class SuccessStep<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger _logger;
    private readonly AdapterTree<TId, TAltId> _adapterTree;
    private readonly IDirtyNodes<TId, TAltId> _dirtyNodes;
    private readonly IIdentitySource<TId> _idSource;
    private readonly NodeUpdateDetection<TId, TAltId> _nodeUpdateDetection;
    private readonly IReadOnlyDictionary<TId, RootInfo<TAltId>> _syncRoots;
    private readonly ICopiedNodes<TId, TAltId> _copiedNodes;
    private readonly IItemExclusionFilter _itemExclusionFilter;

    private readonly FileEditDetectionStep<TId, TAltId> _fileEditDetection = new();

    protected SuccessStep(
        ILogger logger,
        AdapterTree<TId, TAltId> adapterTree,
        IDirtyNodes<TId, TAltId> dirtyNodes,
        IIdentitySource<TId> idSource,
        NodeUpdateDetection<TId, TAltId> nodeUpdateDetection,
        IReadOnlyDictionary<TId, RootInfo<TAltId>> syncRoots,
        ICopiedNodes<TId, TAltId> copiedNodes,
        IItemExclusionFilter itemExclusionFilter)
    {
        _logger = logger;
        _adapterTree = adapterTree;
        _dirtyNodes = dirtyNodes;
        _idSource = idSource;
        _syncRoots = syncRoots;
        _copiedNodes = copiedNodes;
        _nodeUpdateDetection = nodeUpdateDetection;
        _itemExclusionFilter = itemExclusionFilter;
    }

    public void ValidateAndUpdate(
        AdapterTreeNode<TId, TAltId>? node,
        IncomingAdapterTreeNodeModel<TId, TAltId> incomingNodeModel,
        AdapterTreeNode<TId, TAltId> parentNode,
        bool isLogBased = false,
        bool isOneStepMoveExpected = false)
    {
        Ensure.IsTrue(
            parentNode.Id.Equals(incomingNodeModel.ParentId),
            "Node model should match the provided parent node");

        Ensure.IsTrue(
            parentNode.IsRoot || incomingNodeModel.AltId.IsDefault() || incomingNodeModel.AltId.VolumeId == parentNode.AltId.VolumeId,
            "Parent and child nodes belong to different volumes");

        // Log based update detection preserves DirtyAttributes and DirtyParent flags if booth are set.
        // During the state based update detection the node state is known for sure, therefore,
        // node dirtiness flags are removed, but not ones indicating dirtiness of children or descendants.
        const AdapterNodeStatus flags = AdapterNodeStatus.DirtyAttributes | AdapterNodeStatus.DirtyParent;
        var dirtyFlagsToRemove = isLogBased && incomingNodeModel.Status.HasFlag(flags)
            ? (AdapterNodeStatus.DirtyNodeMask & ~flags)
            : AdapterNodeStatus.DirtyNodeMask;

        var status = incomingNodeModel.Status & ~dirtyFlagsToRemove;

        if (node is { IsRoot: true })
        {
            throw new InvalidOperationException("The root tree node cannot be updated");
        }

        if (node != null && !parentNode.Id.Equals(node.Model.ParentId))
        {
            var sourceRoot = node.GetRoot(_syncRoots);
            var destinationRoot = parentNode.GetRoot(_syncRoots);

            // Unexpected move
            if (isLogBased && !isOneStepMoveExpected && !incomingNodeModel.IsLostOrDeleted() && !BranchIsDirty(node))
            {
                // It is suspected, that events arrived disordered.
                // Marking the node as dirty for further state enumeration.
                AppendDirtyStatus(node, AdapterNodeStatus.DirtyAttributes | AdapterNodeStatus.DirtyParent);

                if (BranchIsDirty(parentNode) || parentNode.IsNodeOrBranchDeleted())
                {
                    return;
                }

                CreateDirtyPlaceholder(parentNode, incomingNodeModel.Name);
            }

            // A move between different move scopes
            if (sourceRoot.MoveScope != destinationRoot.MoveScope)
            {
                // A move between different move scopes is replaced with copying + deletion
                CopyAndDelete(node, incomingNodeModel, destinationRoot);

                return;
            }
        }

        var isFolderMove = node?.Type == NodeType.Directory &&
                           !node.Model.ParentId.Equals(parentNode.Id);

        // The folder might not be empty if moved from outside the replica.
        // The folder content might have changed while it was in a dirty branch.
        // The folder content might have changed while it was in a deleted branch.
        if ((node == null && incomingNodeModel.Type == NodeType.Directory) ||
            (isFolderMove && (BranchIsDirty(node!) || node!.IsNodeOrBranchDeleted())))
        {
            status |= AdapterNodeStatus.DirtyDescendants;
        }

        // Checking for cyclic move
        if (isFolderMove && parentNode.FromParentToRoot().SkipLast(1).Any(n => n.Id.Equals(node!.Id)))
        {
            _logger.LogWarning(
                "Moving Adapter Tree node with Id={Id} to parent with Id={ParentId} is a cyclic move",
                node!.Id,
                parentNode.Id);

            AppendDirtyStatus(node, AdapterNodeStatus.DirtyAttributes | AdapterNodeStatus.DirtyParent);

            AppendDirtyStatus(node.Parent!, AdapterNodeStatus.DirtyChildren);

            // All nodes in a path between the desired parent and the current node
            foreach (var tempNode in parentNode.FromParentToRoot().SkipLast(1).TakeWhile(n => !n.Id.Equals(node.Id)))
            {
                AppendDirtyStatus(tempNode, AdapterNodeStatus.DirtyAttributes | AdapterNodeStatus.DirtyParent);
            }

            return;
        }

        node = _fileEditDetection.Execute(node, incomingNodeModel, parentNode);

        var incomingNodeModelWithUpdatedStatus = incomingNodeModel.Copy().WithStatus(status);

        DetectNodeUpdate(node, incomingNodeModelWithUpdatedStatus);
    }

    protected static bool IsDefault([NotNullWhen(false)] TAltId? value)
    {
        return value is null || value.Equals(default);
    }

    protected AdapterTreeNode<TId, TAltId>? ParentNode(LooseCompoundAltIdentity<TAltId> parentId)
    {
        var node = _adapterTree.NodeByAltIdOrDefault(parentId);

        return ExistingNodeOfType(node, NodeType.Directory);
    }

    protected AdapterTreeNode<TId, TAltId>? ExistingNode(AdapterTreeNodeModel<TId, TAltId> nodeModel)
    {
        return ExistingNode(nodeModel.AltId, nodeModel.Type);
    }

    protected AdapterTreeNode<TId, TAltId>? ExistingNode(LooseCompoundAltIdentity<TAltId> nodeAltId, NodeType expectedType)
    {
        var node = ExistingNode(nodeAltId);

        return ExistingNodeOfType(node, expectedType);
    }

    protected AdapterTreeNode<TId, TAltId>? ExistingNode(LooseCompoundAltIdentity<TAltId> nodeAltId)
    {
        return !nodeAltId.IsDefault() ? _adapterTree.NodeByAltIdOrDefault(nodeAltId) : null;
    }

    protected void MarkAsDeleted(AdapterTreeNode<TId, TAltId>? node)
    {
        if (node == null)
        {
            return;
        }

        if (node.IsNodeOrBranchDeleted())
        {
            _logger.LogDebug("Adapter Tree node with Id={Id} is in already deleted branch", node.Id);

            return;
        }

        _logger.LogDebug("Marking Adapter Tree node with Id={Id} as deleted", node.Id);

        // Directories deleted while in a dirty branch are marked with the DirtyDescendants flag.
        var dirtyStatus = node.Type == NodeType.Directory && BranchIsDirty(node)
            ? AdapterNodeStatus.DirtyDeleted | AdapterNodeStatus.DirtyDescendants
            : AdapterNodeStatus.DirtyDeleted;

        AppendDirtyStatus(node, dirtyStatus);
    }

    protected void RemoveAltId(AdapterTreeNode<TId, TAltId> node)
    {
        _logger.LogDebug("Updating Adapter Tree node with Id={Id} to remove AltId value={AltId}", node.Id, node.AltId);

        var updatedNodeModel = IncomingAdapterTreeNodeModel<TId, TAltId>
            .FromNodeModel(node.Model)
            .WithAltId(default);

        DetectNodeUpdate(node, updatedNodeModel);
    }

    protected void AppendDirtyStatus(AdapterTreeNode<TId, TAltId> node, AdapterNodeStatus value)
    {
        _logger.LogDebug("Updating Adapter Tree node with Id={Id} to set status flag(s) ({Flags})", node.Id, value);

        var incoming = IncomingAdapterTreeNodeModel<TId, TAltId>
            .FromNodeModel(node.Model)
            .WithAppendedDirtyFlags(value);

        DetectNodeUpdate(node, incoming);
    }

    protected void SetStateUpdateFlags(AdapterTreeNode<TId, TAltId> node, AdapterNodeStatus value)
    {
        _logger.LogDebug("Updating Adapter Tree node with Id={Id} state update flag(s) to ({Value})", node.Id, value);

        var incoming = IncomingAdapterTreeNodeModel<TId, TAltId>
            .FromNodeModel(node.Model)
            .WithStateUpdateFlags(value);

        DetectNodeUpdate(node, incoming);
    }

    protected void SetDirtyStatus(AdapterTreeNode<TId, TAltId> node, AdapterNodeStatus value)
    {
        var incoming = IncomingAdapterTreeNodeModel<TId, TAltId>
            .FromNodeModel(node.Model)
            .WithDirtyFlags(value);

        DetectNodeUpdate(node, incoming);
    }

    protected void DetectNodeUpdate(AdapterTreeNode<TId, TAltId>? current, IncomingAdapterTreeNodeModel<TId, TAltId>? incoming)
    {
        _nodeUpdateDetection.Execute(current, incoming);
    }

    protected bool BranchIsDirty(AdapterTreeNode<TId, TAltId> node)
    {
        return _dirtyNodes.BranchIsDirty(node);
    }

    protected bool ShouldBeIgnored(
        AdapterTreeNode<TId, TAltId>? existingNode,
        LooseCompoundAltIdentity<TAltId> altId,
        string name,
        FileAttributes attributes,
        PlaceholderState placeholderState,
        [NotNullWhen(false)] AdapterTreeNode<TId, TAltId>? parentNode)
    {
        if (parentNode == null)
        {
            return true;
        }

        var shouldBeIgnored = _itemExclusionFilter.ShouldBeIgnored(name, attributes, placeholderState, parentNode.IsSyncRoot());

        if (!shouldBeIgnored)
        {
            return false;
        }

        var loggingLevel = existingNode?.Model.Status.HasFlag(AdapterNodeStatus.DirtyDeleted) == false || HasAttributesWorthLogging()
            ? LogLevel.Warning
            : LogLevel.Debug;

        _logger.Log(
            loggingLevel,
            "Ignored {Type} \"{Name}\" \"{Root}\"/{Id} {AltId} at parent {ParentId} {ParentAltId}, Attributes=({Attributes}), PlaceholderState=({PlaceholderState})",
            attributes.HasFlag(FileAttributes.Directory) ? NodeType.Directory : NodeType.File,
            _logger.GetSensitiveValueForLogging(name),
            parentNode.GetSyncRoot().Name,
            existingNode != null ? existingNode.Id : null,
            altId,
            parentNode.Id,
            parentNode.AltId,
            attributes,
            placeholderState);

        return true;

        bool HasAttributesWorthLogging()
        {
            return placeholderState.HasFlag(PlaceholderState.Invalid)
                    || attributes.HasFlag(FileAttributes.Device)
                    || (attributes.HasFlag(FileAttributes.ReparsePoint) && !placeholderState.HasFlag(PlaceholderState.Placeholder));
        }
    }

    protected RootInfo<TAltId> GetRoot(AdapterTreeNode<TId, TAltId> node)
    {
        return node.GetRoot(_syncRoots);
    }

    private AdapterTreeNode<TId, TAltId>? ExistingNodeOfType(AdapterTreeNode<TId, TAltId>? node, NodeType expectedType)
    {
        if (node == null || node.Type == expectedType)
        {
            // Node type is expected
            return node;
        }

        // New file system object appeared with the reused ID
        _logger.LogWarning(
            "Adapter Tree node with Id={Id} {AltId} type={Type} doesn't match the expected, marking it as deleted",
            node.Id,
            node.AltId,
            node.Type);

        MarkAsDeleted(node);
        RemoveAltId(node);

        return null;
    }

    private void CreateDirtyPlaceholder(AdapterTreeNode<TId, TAltId> parentNode, string name)
    {
        var dirtyPlaceholderExists = parentNode.ChildrenByName(name).Any(n => n.Model.IsDirtyPlaceholder());
        if (dirtyPlaceholderExists)
        {
            return;
        }

        var dirtyPlaceholder = new IncomingAdapterTreeNodeModel<TId, TAltId>
        {
            Type = NodeType.Directory,
            ParentId = parentNode.Id,
            Name = name,
            Status = AdapterNodeStatus.DirtyPlaceholder | AdapterNodeStatus.DirtyAttributes,
        };

        DetectNodeUpdate(null, dirtyPlaceholder);
    }
}
