using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Adapter.NodeCopying;
using Proton.Drive.Sdk.Sync.Adapter.Shared;
using Proton.Drive.Sdk.Sync.Adapter.Trees.Adapter;
using Proton.Drive.Sdk.Sync.Adapter.UpdateDetection;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Trees.Operations;

namespace Proton.Drive.Sdk.Sync.Adapter.OperationExecution;

internal sealed class SuccessStep<TId, TAltId> : Shared.SuccessStep<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<SuccessStep<TId, TAltId>> _logger;
    private readonly AdapterTree<TId, TAltId> _adapterTree;
    private readonly FileVersionMapping<TId, TAltId> _fileVersionMapping;

    public SuccessStep(
        ILogger<SuccessStep<TId, TAltId>> logger,
        Replica replica,
        AdapterTree<TId, TAltId> adapterTree,
        IDirtyNodes<TId, TAltId> dirtyNodes,
        IIdentitySource<TId> idSource,
        IReadOnlyDictionary<TId, RootInfo<TAltId>> syncRoots,
        ICopiedNodes<TId, TAltId> copiedNodes,
        NodeUpdateDetection<TId, TAltId> nodeUpdateDetection,
        IItemExclusionFilter itemExclusionFilter,
        FileVersionMapping<TId, TAltId> fileVersionMapping)
        : base(logger, replica, adapterTree, dirtyNodes, idSource, nodeUpdateDetection, syncRoots, copiedNodes, itemExclusionFilter)
    {
        _logger = logger;
        _adapterTree = adapterTree;
        _fileVersionMapping = fileVersionMapping;
    }

    public void Execute(ExecutableOperation<TId> operation, NodeInfo<TAltId> finalNodeInfo)
    {
        switch (operation.Type)
        {
            case OperationType.Create:
                ExecuteCreate(operation, finalNodeInfo);
                break;
            default:
                ExecuteOperation(operation, finalNodeInfo);
                break;
        }
    }

    private static bool IsFileTransfer(Operation<AdapterTreeNodeModel<TId, TAltId>> operation)
    {
        return operation is { Type: OperationType.Create, Model.Type: NodeType.File } ||
            operation.Type == OperationType.Edit;
    }

    private void ExecuteCreate(ExecutableOperation<TId> operation, NodeInfo<TAltId> finalNodeInfo)
    {
        // It could happen, that update detection has already processed events of the new file system object
        var existingNode = ExistingNode(finalNodeInfo.GetCompoundId(), operation.Model.Type);
        if (existingNode is not null)
        {
            // We do not expect the existing node to have a correct Id value, so we delete it and create a new one
            _logger.LogWarning(
                "Adapter Tree node with Id={Id} {AltId} type={Type} doesn't match the expected, marking it as deleted",
                existingNode.Id,
                existingNode.AltId,
                existingNode.Type);

            MarkAsDeleted(existingNode);
            RemoveAltId(existingNode);
        }

        ExecuteOperation(operation, finalNodeInfo);
    }

    private void ExecuteOperation(ExecutableOperation<TId> operation, NodeInfo<TAltId> finalNodeInfo)
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
}
