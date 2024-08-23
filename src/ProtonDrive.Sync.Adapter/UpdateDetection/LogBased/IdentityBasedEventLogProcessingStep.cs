using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Sync.Adapter.NodeCopying;
using ProtonDrive.Sync.Adapter.Shared;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.UpdateDetection.LogBased;

internal class IdentityBasedEventLogProcessingStep<TId, TAltId> : SuccessStep<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<IdentityBasedEventLogProcessingStep<TId, TAltId>> _logger;
    private readonly AdapterTree<TId, TAltId> _adapterTree;
    private readonly FileVersionMapping<TId, TAltId> _fileVersionMapping;
    private readonly IReadOnlyDictionary<TId, RootInfo<TAltId>> _syncRoots;

    public IdentityBasedEventLogProcessingStep(
        ILogger<IdentityBasedEventLogProcessingStep<TId, TAltId>> logger,
        AdapterTree<TId, TAltId> adapterTree,
        IDirtyNodes<TId, TAltId> dirtyNodes,
        IIdentitySource<TId> idSource,
        NodeUpdateDetection<TId, TAltId> nodeUpdateDetection,
        FileVersionMapping<TId, TAltId> fileVersionMapping,
        IReadOnlyDictionary<TId, RootInfo<TAltId>> syncRoots,
        ICopiedNodes<TId, TAltId> copiedNodes,
        IItemExclusionFilter itemExclusionFilter)
        : base(logger, adapterTree, dirtyNodes, idSource, nodeUpdateDetection, syncRoots, copiedNodes, itemExclusionFilter)
    {
        _logger = logger;
        _adapterTree = adapterTree;
        _fileVersionMapping = fileVersionMapping;
        _syncRoots = syncRoots;
    }

    public void Execute(int volumeId, string scope, EventLogEntry<TAltId> entry)
    {
        WithLoggedException(() => ProcessEvent(volumeId, scope, entry));
    }

    private static NodeType ToType(FileAttributes attributes)
    {
        return attributes.HasFlag(FileAttributes.Directory) ? NodeType.Directory : NodeType.File;
    }

    private void ProcessEvent(int volumeId, string scope, EventLogEntry<TAltId> entry)
    {
        _logger.LogDebug(
            "Started processing event on {Volume}/\"{Scope}\": {ChangeType} \"{Path}\"/{ParentId}/{Id}, OldPath=\"{OldPath}\"",
            volumeId,
            scope,
            entry.ChangeType,
            entry.Path,
            entry.ParentId,
            entry.Id,
            entry.OldPath);

        if (entry.ChangeType is not EventLogChangeType.Error and not EventLogChangeType.Skipped && IsDefault(entry.Id))
        {
            throw new ArgumentException($"{nameof(entry.Id)} value cannot be null or empty", nameof(entry));
        }

        if (entry.ChangeType is EventLogChangeType.Error or EventLogChangeType.Skipped && !IsDefault(entry.Id))
        {
            throw new ArgumentException($"{nameof(entry.Id)} value must be null or empty", nameof(entry));
        }

        var altId = entry.GetCompoundId(volumeId);
        var node = ExistingNode(altId, entry);

        if (node?.IsSyncRoot() == true)
        {
            _logger.LogDebug("Node with AltId={AltId} is the sync root, skipping", altId);

            return;
        }

        if (entry.ChangeType != EventLogChangeType.Error &&
            entry.ChangeType != EventLogChangeType.Skipped &&
            IsDefault(entry.Id))
        {
            throw new ArgumentException($"{nameof(entry.Id)} value cannot be null or empty", nameof(entry));
        }

        switch (entry.ChangeType)
        {
            case EventLogChangeType.Created:
            case EventLogChangeType.CreatedOrMovedTo:
                OnChanged(volumeId, entry, updatesName: true);
                break;

            case EventLogChangeType.Changed:
                OnChanged(volumeId, entry, updatesName: false);
                break;

            case EventLogChangeType.ChangedOrMoved:
                OnChanged(volumeId, entry, updatesName: true, isOneStepMoveExpected: true);
                break;

            case EventLogChangeType.Moved:
                OnMoved(volumeId, entry);
                break;

            case EventLogChangeType.Deleted:
                // We know the file system object was deleted
                OnDeletedOrMoved(volumeId, entry, deleted: true);
                break;

            case EventLogChangeType.DeletedOrMovedFrom:
                // We don't know whether the file system object was deleted or moved
                OnDeletedOrMoved(volumeId, entry, deleted: false);
                break;

            case EventLogChangeType.Skipped:
                OnSkippedEntries(scope);
                break;

            case EventLogChangeType.Error:
                break;

            default:
                throw new InvalidOperationException();
        }

        _logger.LogDebug(
            "Finished processing event on {Volume}/\"{Scope}\": {ChangeType} \"{Path}\"/{ParentId}/{Id}, OldPath=\"{OldPath}\"",
            volumeId,
            scope,
            entry.ChangeType,
            entry.Path,
            entry.ParentId,
            entry.Id,
            entry.OldPath);
    }

    private void OnMoved(int volumeId, EventLogEntry<TAltId> entry)
    {
        if (IsDefault(entry.ParentId))
        {
            // The destination parent is not specified. This could happen with rename events on Windows local file systems.
            OnDeletedOrMoved(volumeId, entry, deleted: false);

            return;
        }

        OnChanged(volumeId, entry, updatesName: true, isOneStepMoveExpected: true);
    }

    private void OnDeletedOrMoved(int volumeId, EventLogEntry<TAltId> entry, bool deleted)
    {
        var altId = entry.GetCompoundId(volumeId);

        // Remote deletion event entries carry no node type.
        // Therefore, we do not use the entry for checking type of the deleted node.
        var node = deleted ? ExistingNode(altId) : ExistingNode(altId, entry);

        if (node == null)
        {
            _logger.LogDebug("Adapter Tree node with AltId={AltId} does not exist, skipping", altId);

            return;
        }

        if (node.Model.Status.HasFlag(AdapterNodeStatus.DirtyDeleted))
        {
            _logger.LogDebug("Adapter Tree node with Id={Id} is already deleted, skipping", node.Id);

            return;
        }

        if (deleted)
        {
            MarkAsDeleted(node);
        }
        else
        {
            AppendDirtyStatus(node, AdapterNodeStatus.DirtyParent);
            SetStateUpdateFlags(node, GetStateUpdateFlags(node, entry));
        }
    }

    private void OnSkippedEntries(string scope)
    {
        _logger.LogInformation("Event log entries were skipped, marking Adapter Tree root nodes for event scope \"{Scope}\" as dirty", scope);

        var syncRootNodes = _adapterTree.Root.Children.Where(child =>
            _syncRoots.Any(pair => pair.Key.Equals(child.Id) && pair.Value.EventScope == scope)
            && child.Type == NodeType.Directory /* should always be true, defensive programming */
            && !child.Model.IsDirtyPlaceholder() /* should always be true, defensive programming */
            && !child.Model.IsLostOrDeleted() /* should always be true, defensive programming */);

        var hasAffectedSyncRoots = false;

        foreach (var syncRootNode in syncRootNodes)
        {
            SetDirtyStatus(syncRootNode, AdapterNodeStatus.DirtyDescendants);
            hasAffectedSyncRoots = true;
        }

        if (!hasAffectedSyncRoots)
        {
            _logger.LogError("There is no Adapter Tree root nodes for event scope \"{Scope}\"", scope);
        }
    }

    private void OnChanged(int volumeId, EventLogEntry<TAltId> entry, bool updatesName, bool isOneStepMoveExpected = false)
    {
        var altId = entry.GetCompoundId(volumeId);
        var node = ExistingNode(altId, entry);

        var name = updatesName
            ? entry.Name
            : node?.Name ?? entry.Name;

        if (string.IsNullOrEmpty(name))
        {
            throw new InvalidOperationException("Node must have a name");
        }

        var parentNode = !IsDefault(entry.ParentId) ? ParentNode(entry.GetCompoundParentId(volumeId)) : node?.Parent;

        if (ShouldBeIgnored(node, altId, name, entry.Attributes, entry.PlaceholderState, parentNode))
        {
            MarkAsDeleted(node);

            return;
        }

        var model = new IncomingAdapterTreeNodeModel<TId, TAltId>
        {
            Type = ToType(entry.Attributes),
            ParentId = parentNode.Id,
            AltId = altId,
            Name = name,
            RevisionId = entry.RevisionId,
            LastWriteTime = entry.LastWriteTimeUtc,
            Size = entry.Size ?? entry.SizeOnStorage ?? 0L,
            SizeOnStorage = entry.SizeOnStorage,
            ContentVersion = GetMappedVersion(node, entry),
            Status = node?.Model.Status ?? AdapterNodeStatus.None,
        }
            .WithStateUpdateFlags(GetStateUpdateFlags(parentNode, entry));

        ValidateAndUpdate(node, model, parentNode, isLogBased: true, isOneStepMoveExpected);
    }

    private AdapterTreeNode<TId, TAltId>? ExistingNode(LooseCompoundAltIdentity<TAltId> altId, EventLogEntry<TAltId> entry)
    {
        return ExistingNode(altId, ToType(entry.Attributes));
    }

    private long GetMappedVersion(IIdentifiable<TId>? node, EventLogEntry<TAltId> entry)
    {
        return node == null || entry.Size == null ? default : _fileVersionMapping.GetVersion(node.Id, entry.LastWriteTimeUtc, entry.Size.Value);
    }

    private AdapterNodeStatus GetStateUpdateFlags(AdapterTreeNode<TId, TAltId> nodeForObtainingRoot, EventLogEntry<TAltId> entry)
    {
        return entry.PlaceholderState.GetStateUpdateFlags(entry.Attributes, GetRoot(nodeForObtainingRoot));
    }

    private void WithLoggedException(Action origin)
    {
        _logger.WithLoggedException(origin, includeStackTrace: true);
    }
}
