using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ProtonDrive.Sync.Adapter.Shared;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Adapter.UpdateDetection;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.OperationExecution;

internal class SuccessStep<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<SuccessStep<TId, TAltId>> _logger;
    private readonly AdapterTree<TId, TAltId> _adapterTree;
    private readonly IReadOnlyDictionary<TId, RootInfo<TAltId>> _syncRoots;
    private readonly FileVersionMapping<TId, TAltId> _fileVersionMapping;

    public SuccessStep(
        ILogger<SuccessStep<TId, TAltId>> logger,
        AdapterTree<TId, TAltId> adapterTree,
        IReadOnlyDictionary<TId, RootInfo<TAltId>> syncRoots,
        FileVersionMapping<TId, TAltId> fileVersionMapping)
    {
        _logger = logger;
        _adapterTree = adapterTree;
        _syncRoots = syncRoots;
        _fileVersionMapping = fileVersionMapping;
    }

    public void Execute(ExecutableOperation<TId> operation, NodeInfo<TAltId> finalNodeInfo)
    {
        var mappedOperation = ToAdapterTreeOperation(operation, finalNodeInfo);

        ExecuteOnTree(mappedOperation);
        AddToFileVersionMapping(mappedOperation);
    }

    private Operation<AdapterTreeNodeModel<TId, TAltId>> ToAdapterTreeOperation(ExecutableOperation<TId> operation, NodeInfo<TAltId> nodeInfo)
    {
        return new Operation<AdapterTreeNodeModel<TId, TAltId>>(
            operation.Type,
            ToAdapterTreeNodeModel(operation, nodeInfo));
    }

    private AdapterTreeNodeModel<TId, TAltId> ToAdapterTreeNodeModel(ExecutableOperation<TId> operation, NodeInfo<TAltId> nodeInfo)
    {
        var model = operation.Model;
        var containsParentId = operation.Type is OperationType.Create or OperationType.Move;
        var nodeForObtainingRoot = _adapterTree.NodeById(containsParentId ? model.ParentId : model.Id);

        return new AdapterTreeNodeModel<TId, TAltId>()
            .CopiedFrom(model)
            .WithAltId(nodeInfo.GetCompoundId())
            .WithRevisionId(nodeInfo.RevisionId)
            .WithLastWriteTime(nodeInfo.LastWriteTimeUtc)
            .WithSize(nodeInfo.Size)
            .WithStateUpdateFlags(GetStateUpdateFlags(nodeForObtainingRoot, nodeInfo));
    }

    private AdapterNodeStatus GetStateUpdateFlags(AdapterTreeNode<TId, TAltId> nodeForObtainingRoot, NodeInfo<TAltId> nodeInfo)
    {
        return nodeInfo.PlaceholderState.GetStateUpdateFlags(nodeInfo.Attributes, GetRoot(nodeForObtainingRoot));
    }

    private RootInfo<TAltId> GetRoot(AdapterTreeNode<TId, TAltId> node)
    {
        return node.GetRoot(_syncRoots);
    }

    private void ExecuteOnTree(Operation<AdapterTreeNodeModel<TId, TAltId>> operation)
    {
        _adapterTree.Operations.LogAndExecute(_logger, operation);
    }

    private void AddToFileVersionMapping(Operation<AdapterTreeNodeModel<TId, TAltId>> operation)
    {
        if (IsFileTransfer(operation))
        {
            _fileVersionMapping.Add(operation.Model);
        }
    }

    private bool IsFileTransfer(Operation<AdapterTreeNodeModel<TId, TAltId>> operation)
    {
        return (operation.Type == OperationType.Create && operation.Model.Type == NodeType.File) ||
               operation.Type == OperationType.Edit;
    }
}
