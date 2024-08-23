using System;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.Trees.Dirty;

public static class DirtyTreeNodeModelExtensions
{
    private const string UnusedTreeNodeName = ".";

    public static DirtyTreeNodeModel<TId> WithStatus<TId>(this DirtyTreeNodeModel<TId> nodeModel, AdapterNodeStatus value)
        where TId : IEquatable<TId>
    {
        nodeModel.Status = value;

        return nodeModel;
    }

    public static bool IsDirtyPlaceholder<TId>(this DirtyTreeNodeModel<TId> nodeModel)
        where TId : IEquatable<TId>
    {
        return nodeModel.Status.HasFlag(AdapterNodeStatus.DirtyPlaceholder);
    }

    public static bool HasDirtyChildrenFlag<TId>(this DirtyTreeNodeModel<TId> nodeModel)
        where TId : IEquatable<TId>
    {
        return nodeModel.Status.HasFlag(AdapterNodeStatus.DirtyChildren);
    }

    public static bool HasDirtyDescendantsFlag<TId>(this DirtyTreeNodeModel<TId> nodeModel)
        where TId : IEquatable<TId>
    {
        return nodeModel.Status.HasFlag(AdapterNodeStatus.DirtyDescendants);
    }

    public static bool IsLostOrDeleted<TId>(this DirtyTreeNodeModel<TId> nodeModel)
        where TId : IEquatable<TId>
    {
        return nodeModel.Status.HasAnyFlag(AdapterNodeStatus.DirtyParent | AdapterNodeStatus.DirtyDeleted);
    }

    public static bool IsCandidateForDirtyTree<TId, TAltId>(
        this AdapterTreeNodeModel<TId, TAltId> nodeModel)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return IsCandidateForDirtyTree(nodeModel.Status);
    }

    public static bool IsCandidateForDirtyTree<TId>(
        this DirtyTreeNodeModel<TId> nodeModel)
        where TId : IEquatable<TId>
    {
        return IsCandidateForDirtyTree(nodeModel.Status);
    }

    public static DirtyTreeNodeModel<TId> ToDirtyTreeNodeModel<TId, TAltId>(
        this AdapterTreeNodeModel<TId, TAltId> nodeModel)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        // The node names are not used in the Dirty Tree, but they must be not empty for operations to execute.
        // Therefore, all nodes are directories despite corresponding Adapter Tree node might be a file.
        return new DirtyTreeNodeModel<TId>()
                .WithId(nodeModel.Id)
                .WithParentId(nodeModel.ParentId)
                .WithName<DirtyTreeNodeModel<TId>, TId>(UnusedTreeNodeName)
                .WithStatus(nodeModel.Status & AdapterNodeStatus.DirtyMask);
    }

    private static bool IsCandidateForDirtyTree(AdapterNodeStatus status)
    {
        return status.HasAnyFlag(AdapterNodeStatus.DirtyMask);
    }
}
