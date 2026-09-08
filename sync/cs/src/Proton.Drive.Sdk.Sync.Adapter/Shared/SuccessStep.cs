using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Adapter.NodeCopying;
using Proton.Drive.Sdk.Sync.Adapter.Trees.Adapter;
using Proton.Drive.Sdk.Sync.Adapter.UpdateDetection;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Ignore;
using Proton.Drive.Sdk.Sync.Shared.Trees;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.IO;
using Proton.Drive.Shared.Logging;

namespace Proton.Drive.Sdk.Sync.Adapter.Shared;

internal abstract partial class SuccessStep<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger _logger;
    private readonly Replica _replica;
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
        Replica replica,
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
        _replica = replica;
    }

    protected virtual bool DirtyParentIsPreservedOnLogBasedUpdate => false;

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

        // Log-based update detection preserves DirtyAttributes if both DirtyAttributes and DirtyParent flags are set.
        // During the state-based update detection the node state is known for sure, therefore,
        // node dirtiness flags are removed, but not ones indicating dirtiness of children or descendants.
        // If DirtyParent is not always removed, state-based detection risks causing deletion of nodes
        // that were moved or renamed during the traversal, from a not-yet-visited part of the tree to an already-visited one.
        const AdapterNodeStatus flags = AdapterNodeStatus.DirtyAttributes | AdapterNodeStatus.DirtyParent;
        var flagsToPreserve = DirtyParentIsPreservedOnLogBasedUpdate ? flags : AdapterNodeStatus.DirtyAttributes;
        var dirtyFlagsToRemove = isLogBased && incomingNodeModel.Status.HasFlag(flags)
            ? (AdapterNodeStatus.DirtyNodeMask & ~flagsToPreserve)
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
                "Moving {Replica} Adapter Tree {Type} node with Id={Id} to parent with Id={ParentId} is a cyclic move",
                _replica,
                node!.Type,
                node.Id,
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
            _logger.LogDebug(
                "{Replica} Adapter Tree {Type} node with Id={Id} at parent with Id={ParentId} is in already deleted branch",
                _replica,
                node.Type,
                node.Id,
                node.Model.ParentId);

            return;
        }

        _logger.LogDebug(
            "Marking {Replica} Adapter Tree {Type} node with Id={Id} at parent with Id={ParentId} as deleted",
            _replica,
            node.Type,
            node.Id,
            node.Model.ParentId);

        // Directories deleted while in a dirty branch are marked with the DirtyDescendants flag.
        var dirtyStatus = node.Type == NodeType.Directory && BranchIsDirty(node)
            ? AdapterNodeStatus.DirtyDeleted | AdapterNodeStatus.DirtyDescendants
            : AdapterNodeStatus.DirtyDeleted;

        AppendDirtyStatus(node, dirtyStatus);
    }

    protected void RemoveAltId(AdapterTreeNode<TId, TAltId> node)
    {
        _logger.LogDebug(
            "Updating {Replica} Adapter Tree {Type} node with Id={Id} at parent with Id={ParentId} to remove AltId value={AltId}",
            _replica,
            node.Type,
            node.Id,
            node.Model.ParentId,
            node.AltId);

        var updatedNodeModel = IncomingAdapterTreeNodeModel<TId, TAltId>
            .FromNodeModel(node.Model)
            .WithAltId(default);

        DetectNodeUpdate(node, updatedNodeModel);
    }

    protected void AppendDirtyStatus(AdapterTreeNode<TId, TAltId> node, AdapterNodeStatus value)
    {
        _logger.LogInformation(
            "Updating {Replica} Adapter Tree {Type} node with Id={Id} at parent with Id={ParentId} to append status flag(s) ({Flags})",
            _replica,
            node.Type,
            node.Id,
            node.Model.ParentId,
            value);

        var incoming = IncomingAdapterTreeNodeModel<TId, TAltId>
            .FromNodeModel(node.Model)
            .WithAppendedDirtyFlags(value);

        DetectNodeUpdate(node, incoming);
    }

    protected void SetStateUpdateFlags(AdapterTreeNode<TId, TAltId> node, AdapterNodeStatus value)
    {
        _logger.LogDebug(
            "Updating {Replica} Adapter Tree {Type} node with Id={Id} at parent with Id={ParentId} state update flag(s) to ({Value})",
            _replica,
            node.Type,
            node.Id,
            node.Model.ParentId,
            value);

        var incoming = IncomingAdapterTreeNodeModel<TId, TAltId>
            .FromNodeModel(node.Model)
            .WithStateUpdateFlags(value);

        DetectNodeUpdate(node, incoming);
    }

    protected void SetDirtyStatus(AdapterTreeNode<TId, TAltId> node, AdapterNodeStatus value)
    {
        _logger.LogInformation(
            "Updating {Replica} Adapter Tree {Type} node with Id={Id} at parent with Id={ParentId} to set status flag(s) ({Flags})",
            _replica,
            node.Type,
            node.Id,
            node.Model.ParentId,
            value);

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

        var decision = _itemExclusionFilter.GetDecision(
            name,
            attributes,
            placeholderState,
            parentNode.IsSyncRoot(),
            GetIgnoreRuleScope(parentNode, name));

        if (decision == ItemExclusionDecision.Include)
        {
            return false;
        }

        if (decision == ItemExclusionDecision.ExcludeByUserRule &&
            existingNode != null &&
            _adapterTree.NameEqualityComparer.Equals(existingNode.Name, name))
        {
            // The item is already indexed and its name has not changed, so what changed is the rule
            // set. Honouring the rule now would be reported to the Sync Engine as a deletion, and the
            // Sync Engine would delete the item from the opposite replica. A rule that starts
            // matching must never cost data, so the item keeps being synced.
            //
            // A rename into an ignored name is a different matter and deliberately not covered here.
            // There the user moved the item out of the synchronized name space, which is the same
            // thing that happens when something is renamed to a temporary file name, and the
            // built-in exclusions have always reported that as a deletion.
            _logger.LogInformation(
                "{Replica} {Type} \"{Name}\" \"{Root}\"/{Id} matches a user defined ignore rule, but is already indexed and keeps being synced",
                _replica,
                existingNode.Type,
                _logger.GetSensitiveValueForLogging(name),
                parentNode.GetSyncRoot().Name,
                existingNode.Id);

            return false;
        }

        var loggingLevel = existingNode?.Model.Status.HasFlag(AdapterNodeStatus.DirtyDeleted) == false || HasAttributesWorthLogging()
            ? LogLevel.Warning
            : LogLevel.Debug;

        _logger.Log(
            loggingLevel,
            "Ignored {Replica} {Type} \"{Name}\" \"{Root}\"/{Id} {AltId} at parent {ParentId} {ParentAltId}, Attributes=({Attributes}), PlaceholderState=({PlaceholderState})",
            _replica,
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

    private IgnoreRuleScope GetIgnoreRuleScope(AdapterTreeNode<TId, TAltId> parentNode, string name)
    {
        if (!_itemExclusionFilter.HonoursUserRules)
        {
            return IgnoreRuleScope.None;
        }

        // Sync roots themselves are never matched against ignore rules
        if (parentNode.IsRoot)
        {
            return IgnoreRuleScope.None;
        }

        if (!_syncRoots.TryGetValue(parentNode.GetSyncRoot().Id, out var root) || string.IsNullOrEmpty(root.LocalPath))
        {
            return IgnoreRuleScope.None;
        }

        var (_, parentPath) = parentNode.Path();

        var relativePath = parentPath.Length == 0
            ? name
            : parentPath + System.IO.Path.DirectorySeparatorChar + name;

        return new IgnoreRuleScope(root.Id, root.LocalPath, relativePath);
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
            "{Replica} Adapter Tree {Type} node with Id={Id} {AltId} at parent with Id={ParentId} doesn't match the expected, marking it as deleted",
            _replica,
            node.Type,
            node.Id,
            node.AltId,
            node.Model.ParentId);

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
