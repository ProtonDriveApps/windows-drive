using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.OnDemandHydration.FileSizeCorrection;

internal sealed class PreconditionsValidationStep<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<PreconditionsValidationStep<TId, TAltId>> _logger;
    private readonly AdapterTree<TId, TAltId> _adapterTree;

    public PreconditionsValidationStep(
        ILogger<PreconditionsValidationStep<TId, TAltId>> logger,
        AdapterTree<TId, TAltId> adapterTree)
    {
        _logger = logger;
        _adapterTree = adapterTree;
    }

    public bool Execute(AdapterTreeNodeModel<TId, TAltId> initialNodeModel)
    {
        var node = _adapterTree.NodeByIdOrDefault(initialNodeModel.Id);

        return NodeExists(node, initialNodeModel) &&
               !NodeHasDiverged(node, initialNodeModel) &&
               !NodeOrBranchIsDeleted(node);
    }

    public bool ExecuteBeforeApplyingResult(ExecutableOperation<TId> operation)
    {
        var node = _adapterTree.NodeByIdOrDefault(operation.Model.Id);

        return NodeExists(node, operation.Model);
    }

    private static bool IsNullOrDefault<T>([NotNullWhen(false)] T? value)
        where T : IEquatable<T>
    {
        return value is null || value.Equals(default);
    }

    private bool NodeExists([NotNullWhen(true)] AdapterTreeNode<TId, TAltId>? node, FileSystemNodeModel<TId> initialNodeModel)
    {
        if (node is not null)
        {
            return true;
        }

        _logger.LogDebug("Adapter Tree node with Id={Id} does not exist", initialNodeModel.Id);

        return false;
    }

    private bool NodeHasDiverged(AdapterTreeNode<TId, TAltId> node, AdapterTreeNodeModel<TId, TAltId> nodeModel)
    {
        if (IsNullOrDefault(node.AltId) || !node.AltId.Equals(nodeModel.AltId))
        {
            _logger.LogDebug(
                "Adapter Tree node with Id={Id} external Id value has diverged from expected {ExpectedAltId} to {AltId}",
                nodeModel.Id,
                nodeModel.AltId,
                node.AltId);

            return true;
        }

        if (node.Model.Size != nodeModel.Size || node.Model.LastWriteTime != nodeModel.LastWriteTime || node.Model.ContentVersion != nodeModel.ContentVersion)
        {
            _logger.LogDebug(
                "Adapter Tree node with Id={Id} has diverged to Size={Size}, LastWriteTime={LastWriteTime:O}, ContentVersion={ContentVersion}",
                nodeModel.Id,
                node.Model.Size,
                node.Model.LastWriteTime,
                node.Model.ContentVersion);

            return true;
        }

        return false;
    }

    private bool NodeOrBranchIsDeleted(AdapterTreeNode<TId, TAltId> node)
    {
        if (!node.IsNodeOrBranchDeleted())
        {
            return false;
        }

        _logger.LogDebug("Adapter Tree node with Id={Id} {AltId} or branch is deleted", node.Id, node.AltId);

        return true;
    }
}
