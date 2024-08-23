using System;
using System.Collections.Generic;
using System.IO;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.OperationExecution;

internal class PreparationStep<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly AdapterTree<TId, TAltId> _adapterTree;
    private readonly IReadOnlyDictionary<TId, RootInfo<TAltId>> _syncRoots;

    public PreparationStep(
        AdapterTree<TId, TAltId> adapterTree,
        IReadOnlyDictionary<TId, RootInfo<TAltId>> syncRoots)
    {
        _adapterTree = adapterTree;
        _syncRoots = syncRoots;
    }

    public (NodeInfo<TAltId> NodeInfo, NodeInfo<TAltId>? DestinationInfo) Execute(ExecutableOperation<TId> operation)
    {
        return operation.Type switch
        {
            OperationType.Create => (PrepareCreate(operation.Model), null),
            OperationType.Edit => (PrepareEdit(operation.Model, operation.Backup), null),
            OperationType.Move => PrepareMove(operation.Model),
            OperationType.Delete => (PrepareDelete(operation.Model), null),
            _ => throw new InvalidOperationException(),
        };
    }

    private NodeInfo<TAltId> PrepareCreate(IFileSystemNodeModel<TId> model)
    {
        var parentNode = _adapterTree.DirectoryById(model.ParentId);
        var nodeInfo = ToNodeInfo(parentNode, model);

        return nodeInfo;
    }

    private NodeInfo<TAltId> PrepareEdit(IFileSystemNodeModel<TId> model, bool backup)
    {
        var node = _adapterTree.FileById(model.Id);
        var nodeInfo = node.ToNodeInfo(_syncRoots);

        if (backup)
        {
            // Archive file attribute indicates the file should be backed up before overwriting
            nodeInfo = nodeInfo.WithAttributes(nodeInfo.Attributes | FileAttributes.Archive);
        }

        return nodeInfo;
    }

    private (NodeInfo<TAltId> Source, NodeInfo<TAltId> Destination) PrepareMove(IFileSystemNodeModel<TId> model)
    {
        var node = AdapterTreeNode(model);
        var nodeInfo = node.ToNodeInfo(_syncRoots);
        NodeInfo<TAltId> destinationInfo;

        if (node.Model.ParentId.Equals(model.ParentId))
        {
            // Rename
            destinationInfo = nodeInfo.Copy()
                .WithName(model.Name)
                .WithPath(string.Empty);
        }
        else
        {
            // Move
            var destinationParent = _adapterTree.DirectoryById(model.ParentId);
            destinationInfo = ToNodeInfo(node, destinationParent, model);
        }

        return (nodeInfo, destinationInfo);
    }

    private NodeInfo<TAltId> PrepareDelete(IFileSystemNodeModel<TId> model)
    {
        var node = AdapterTreeNode(model);
        var nodeInfo = node.ToNodeInfo(_syncRoots);

        return nodeInfo;
    }

    private AdapterTreeNode<TId, TAltId> AdapterTreeNode(IFileSystemNodeModel<TId> model)
    {
        var node = _adapterTree.NodeByIdOrDefault(model.Id);
        if (node == null)
        {
            throw new TreeException($"Node with Id={model.Id} does not exist in the AdapterTree");
        }

        return node;
    }

    private NodeInfo<TAltId> ToNodeInfo(AdapterTreeNode<TId, TAltId> parentNode, IFileSystemNodeModel<TId> nodeModel)
    {
        var (root, parentPath) = parentNode.Path(_syncRoots);
        var path = Path.Combine(parentPath, nodeModel.Name);

        return new NodeInfo<TAltId>()
            .WithParentId(parentNode.AltId.ItemId)
            .WithRoot(root)
            .WithPath(path)
            .WithName(nodeModel.Name)
            .WithAttributes(nodeModel.Type == NodeType.Directory ? FileAttributes.Directory : default);
    }

    private NodeInfo<TAltId> ToNodeInfo(AdapterTreeNode<TId, TAltId> node, AdapterTreeNode<TId, TAltId> parentNode, IFileSystemNodeModel<TId> nodeModel)
    {
        var (root, parentPath) = parentNode.Path(_syncRoots);
        var path = Path.Combine(parentPath, nodeModel.Name);

        return new NodeInfo<TAltId>()
            .WithId(node.AltId.ItemId)
            .WithParentId(parentNode.AltId.ItemId)
            .WithRoot(root)
            .WithPath(path)
            .WithName(nodeModel.Name)
            .WithRevisionId(node.Model.RevisionId)
            .WithAttributes(node.Model.Type == NodeType.Directory ? FileAttributes.Directory : default)
            .WithLastWriteTimeUtc(node.Model.LastWriteTime)
            .WithSize(node.Model.Size);
    }
}
