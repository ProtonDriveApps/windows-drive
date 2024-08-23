using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.Adapters;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.OperationExecution;

internal sealed class NameConflictStep<TId, TAltId>
    where TId : struct, IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<NameConflictStep<TId, TAltId>> _logger;
    private readonly AdapterTree<TId, TAltId> _adapterTree;
    private readonly PreconditionsValidationStep<TId, TAltId> _preconditionsValidation;

    public NameConflictStep(
        ILogger<NameConflictStep<TId, TAltId>> logger,
        AdapterTree<TId, TAltId> adapterTree,
        PreconditionsValidationStep<TId, TAltId> preconditionsValidation)
    {
        _logger = logger;
        _adapterTree = adapterTree;
        _preconditionsValidation = preconditionsValidation;
    }

    public ExecutionResult<TId>? Execute(
        ExecutableOperation<TId> operation,
        Exception exception)
    {
        if (operation.Type is not OperationType.Create and not OperationType.Move)
        {
            return default;
        }

        if (exception is not FileSystemClientException<TAltId> { ErrorCode: FileSystemErrorCode.DuplicateName })
        {
            return default;
        }

        var parentNode = _adapterTree.NodeByIdOrDefault(operation.Model.ParentId);

        return
            _preconditionsValidation.ExecuteBeforeNameConflict(operation) ??
            ProcessNameConflict(operation, parentNode);
    }

    private ExecutionResult<TId> ProcessNameConflict(
        ExecutableOperation<TId> operation,
        AdapterTreeNode<TId, TAltId>? parentNode)
    {
        if (parentNode == null)
        {
            return ExecutionResult<TId>.Failure(operation.Type is OperationType.Move ? ExecutionResultCode.DirtyDestination : ExecutionResultCode.DirtyBranch);
        }

        var node = GetConflictingNode(operation, parentNode);

        if (node == null)
        {
            // We don't know the ID of conflicting node
            return ExecutionResult<TId>.Failure(ExecutionResultCode.NameConflict);
        }

        _logger.LogDebug("A conflicting node with Id={Id} exists", node.Id);

        return ExecutionResult<TId>.Failure(ExecutionResultCode.NameConflict, node.Id);
    }

    private AdapterTreeNode<TId, TAltId>? GetConflictingNode(
        ExecutableOperation<TId> operation,
        AdapterTreeNode<TId, TAltId> parentNode)
    {
        var model = operation.Model;
        var conflictingNodes = parentNode.ChildrenByName(model.Name);

        return conflictingNodes.FirstOrDefault(n => !n.Id.Equals(model.Id) && !n.Model.IsLostOrDeleted());
    }
}
