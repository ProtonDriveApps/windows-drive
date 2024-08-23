using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Extensions.Logging;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Trees.Changes;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.UpdateDetection;

internal class NodeUpdateDetection<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<NodeUpdateDetection<TId, TAltId>> _logger;
    private readonly IIdentitySource<TId> _idSource;
    private readonly AdapterTree<TId, TAltId> _adapterTree;
    private readonly IDetectedTreeChanges<TId> _detectedUpdates;
    private readonly IIdentitySource<long> _contentVersionSequence;
    private readonly UpdateLogging<TId, TAltId> _updateLogging;

    private readonly EqualizeOperationsFactory<AdapterTreeNodeModel<TId, TAltId>, TId> _equalizeOperationsFactory;

    public NodeUpdateDetection(
        ILogger<NodeUpdateDetection<TId, TAltId>> logger,
        IIdentitySource<TId> idSource,
        AdapterTree<TId, TAltId> adapterTree,
        IDetectedTreeChanges<TId> detectedUpdates,
        IIdentitySource<long> contentVersionSequence,
        UpdateLogging<TId, TAltId> updateLogging)
    {
        _logger = logger;
        _idSource = idSource;
        _adapterTree = adapterTree;
        _detectedUpdates = detectedUpdates;
        _contentVersionSequence = contentVersionSequence;
        _updateLogging = updateLogging;

        _equalizeOperationsFactory = new EqualizeOperationsFactory<AdapterTreeNodeModel<TId, TAltId>, TId>(
            new AdapterTreeNodeModelMetadataEqualityComparer<TId, TAltId>());
    }

    public AdapterTreeNodeModel<TId, TAltId>? Execute(
        AdapterTreeNode<TId, TAltId>? currentNode,
        IncomingAdapterTreeNodeModel<TId, TAltId>? incomingNodeModel)
    {
        if (IsMove(currentNode, incomingNodeModel))
        {
            // Cannot move sync root
            if (currentNode.IsSyncRoot())
            {
                throw new InvalidOperationException("Cannot move sync root node");
            }

            // Cannot move to root
            if (incomingNodeModel.ParentId.Equals(_adapterTree.Root.Id))
            {
                throw new InvalidOperationException("Cannot move to root");
            }
        }

        var current = currentNode?.Model;
        var model = WithSyncRootDirtyFlagRestrictions(WithIdAndContentVersion(current, incomingNodeModel));

        ApplyUpdates(current, model);

        return incomingNodeModel;
    }

    private static bool IsMove(
        [NotNullWhen(true)]
        AdapterTreeNode<TId, TAltId>? currentNode,
        [NotNullWhen(true)]
        AdapterTreeNodeModel<TId, TAltId>? incomingNodeModel)
    {
        return currentNode != null && incomingNodeModel != null && !currentNode.Model.ParentId.Equals(incomingNodeModel.ParentId);
    }

    private AdapterTreeNodeModel<TId, TAltId>? WithSyncRootDirtyFlagRestrictions(AdapterTreeNodeModel<TId, TAltId>? incomingNodeModel)
    {
        if (incomingNodeModel == null)
        {
            return null;
        }

        if (!incomingNodeModel.ParentId.Equals(_adapterTree.Root.Id))
        {
            return incomingNodeModel;
        }

        // Sync root node itself cannot be dirty, but it can have
        // DirtyChildren and DirtyDescendants status flags set
        const AdapterNodeStatus restrictedDirtyFlags = AdapterNodeStatus.DirtyNodeMask;

        if (incomingNodeModel.Status.HasAnyFlag(restrictedDirtyFlags))
        {
            _logger.LogWarning(
                "Attempted to set restricted dirty flags ({DirtyFlags}) on sync root node with Id={Id}",
                incomingNodeModel.Status & restrictedDirtyFlags,
                incomingNodeModel.Id);
        }

        return incomingNodeModel.WithRemovedFlags(restrictedDirtyFlags);
    }

    private AdapterTreeNodeModel<TId, TAltId>? WithIdAndContentVersion(
        AdapterTreeNodeModel<TId, TAltId>? currentNodeModel,
        IncomingAdapterTreeNodeModel<TId, TAltId>? incomingNodeModel)
    {
        var model = incomingNodeModel == null
            ? null
            : currentNodeModel != null
                /* Exists in the tree */
                ? incomingNodeModel
                    .WithId(currentNodeModel.Id)
                    .WithContentVersion(GetContentVersion(currentNodeModel, incomingNodeModel))
                /* Doesn't exist in the tree */
                : incomingNodeModel
                    .WithId(incomingNodeModel.Id.Equals(default) ? _idSource.NextValue() : incomingNodeModel.Id)
                    .WithContentVersion(GetContentVersion(incomingNodeModel));

        return model;
    }

    private long GetContentVersion(AdapterTreeNodeModel<TId, TAltId> currentNodeModel, IncomingAdapterTreeNodeModel<TId, TAltId> incomingNodeModel)
    {
        return incomingNodeModel.ContentVersion != default
            /* File content version is specified, take it */
            ? incomingNodeModel.ContentVersion
            /* File content version is not specified */
            : incomingNodeModel.Type == NodeType.File && HasContentChanged(currentNodeModel, incomingNodeModel)
                /* File content has changed and file content version was not yet updated */
                ? _contentVersionSequence.NextValue()
                /* File content has not changed */
                : currentNodeModel.ContentVersion;
    }

    private bool HasContentChanged(
        AdapterTreeNodeModel<TId, TAltId> currentNodeModel,
        IncomingAdapterTreeNodeModel<TId, TAltId> incomingNodeModel)
    {
        // When the RevisionId is available, a change to it is interpreted as a file content change.
        // Remote files, discovered by the app version 1.4.0 and earlier, have no RevisionId (it's NULL) in the Adapter Tree.
        if (incomingNodeModel.RevisionId != null &&
            currentNodeModel.RevisionId != null)
        {
            return incomingNodeModel.RevisionId != currentNodeModel.RevisionId;
        }

        // When the RevisionId is not available, a change to LastWriteTime or Size is interpreted as a file content change, except when
        // the previous LastWriteTime has default value.
        // Local files always have LastWriteTime and Size defined, but SizeOnStorage and RevisionId are null.
        // Remote files, discovered by the app version 1.4.1 and earlier, have no LastWriteTime value (it's default) in the Adapter Tree.
        // Remote files, discovered by the app version 1.4.2 and later, have LastWriteTime value in the Adapter Tree. When the ModificationTime
        // is missing in remote file extended attributes, the remote link modification time is used.
        // Incoming remote file node models always have RevisionId, LastWriteTime, and SizeOnStorage values, Size is optional.
        // When the Size is missing in remote file extended attributes, SizeOnStorage is used.
        return (currentNodeModel.LastWriteTime != default && incomingNodeModel.LastWriteTime != currentNodeModel.LastWriteTime) ||
               (incomingNodeModel.Size != currentNodeModel.Size && incomingNodeModel.SizeOnStorage != currentNodeModel.Size);
    }

    private long GetContentVersion(AdapterTreeNodeModel<TId, TAltId> incomingNodeModel)
    {
        return incomingNodeModel.Type == NodeType.File
            /* A new file */
            ? incomingNodeModel.ContentVersion != default
                /* File content version already set */
                ? incomingNodeModel.ContentVersion
                /* File content version not set */
                : _contentVersionSequence.NextValue()
            /* A new folder */
            : default;
    }

    private void ApplyUpdates(
        AdapterTreeNodeModel<TId, TAltId>? currentNodeModel,
        AdapterTreeNodeModel<TId, TAltId>? incomingNodeModel)
    {
        var any = false;
        var operations = _equalizeOperationsFactory.Operations(currentNodeModel, incomingNodeModel).ToList();

        foreach (var operation in operations)
        {
            any = true;

            Execute(operation);
        }

        AddDetectedUpdates(operations, currentNodeModel, incomingNodeModel);

        if (!any)
        {
            _logger.LogDebug("No changes to Adapter Tree are needed");
        }
    }

    private void Execute(Operation<AdapterTreeNodeModel<TId, TAltId>> operation)
    {
        _adapterTree.Operations.LogAndExecute(_logger, operation);
    }

    private void AddDetectedUpdates(
        IEnumerable<Operation<AdapterTreeNodeModel<TId, TAltId>>> operations,
        AdapterTreeNodeModel<TId, TAltId>? currentNodeModel,
        AdapterTreeNodeModel<TId, TAltId>? incomingNodeModel)
    {
        var transformed = DirtyTransformed(currentNodeModel, incomingNodeModel);
        if (transformed != null)
        {
            AddDetectedUpdate(transformed, currentNodeModel);
        }
        else
        {
            foreach (var operation in operations)
            {
                if (!ShouldBeSentToSyncEngine(operation, currentNodeModel!))
                {
                    continue;
                }

                AddDetectedUpdate(operation, currentNodeModel);
            }
        }
    }

    private Operation<AdapterTreeNodeModel<TId, TAltId>>? DirtyTransformed(
        AdapterTreeNodeModel<TId, TAltId>? currentNodeModel,
        AdapterTreeNodeModel<TId, TAltId>? incomingNodeModel)
    {
        // When dirty placeholder becomes a usual node it is sent to the Sync Engine
        // in a Create operation.
        if (currentNodeModel != null && incomingNodeModel != null &&
            currentNodeModel.IsDirtyPlaceholder() &&
            !incomingNodeModel.IsDirtyPlaceholder())
        {
            return new Operation<AdapterTreeNodeModel<TId, TAltId>>(
                OperationType.Create,
                incomingNodeModel);
        }

        return null;
    }

    private void AddDetectedUpdate(
        Operation<AdapterTreeNodeModel<TId, TAltId>> operation,
        AdapterTreeNodeModel<TId, TAltId>? previousNodeModel)
    {
        _updateLogging.LogDetectedOperation(operation, previousNodeModel);

        var fileSystemOperation = ToFileSystemOperation(operation);
        _updateLogging.LogDetectedUpdate(fileSystemOperation);

        _detectedUpdates.Add(fileSystemOperation);
    }

    private bool ShouldBeSentToSyncEngine(
        Operation<AdapterTreeNodeModel<TId, TAltId>> operation,
        AdapterTreeNodeModel<TId, TAltId> currentNodeModel)
    {
        // Metadata updates are not send to the Sync Engine
        if (operation.Type == OperationType.Update)
        {
            return false;
        }

        // Directory edits are not send to the Sync Engine
        if (operation.Type == OperationType.Edit && currentNodeModel.Type == NodeType.Directory)
        {
            return false;
        }

        // Dirty placeholders are not send to the Sync Engine
        if (operation.Model.IsDirtyPlaceholder() ||
            (operation.Type == OperationType.Delete && currentNodeModel.IsDirtyPlaceholder()))
        {
            return false;
        }

        return true;
    }

    private Operation<FileSystemNodeModel<TId>> ToFileSystemOperation(Operation<AdapterTreeNodeModel<TId, TAltId>> operation)
    {
        return new Operation<FileSystemNodeModel<TId>>(
            operation.Type,
            ToFileSystemNodeModel(operation.Model));
    }

    private FileSystemNodeModel<TId> ToFileSystemNodeModel(AdapterTreeNodeModel<TId, TAltId> nodeModel)
    {
        return new FileSystemNodeModel<TId>().CopiedFrom(nodeModel);
    }
}
