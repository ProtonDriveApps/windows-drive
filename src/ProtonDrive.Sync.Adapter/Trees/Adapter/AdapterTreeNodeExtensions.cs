using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem.Traversal;

namespace ProtonDrive.Sync.Adapter.Trees.Adapter;

internal static class AdapterTreeNodeExtensions
{
    public static (string? RootName, string Path) Path<TId, TAltId>(this AdapterTreeNode<TId, TAltId> node)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        var (syncRootNode, path) = PathWithRoot(node);

        return (syncRootNode?.Name, path);
    }

    public static (RootInfo<TAltId>? Root, string Path) Path<TId, TAltId>(this AdapterTreeNode<TId, TAltId> node, IReadOnlyDictionary<TId, RootInfo<TAltId>> syncRoots)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        var (syncRootNode, path) = PathWithRoot(node);

        return (syncRootNode != null ? syncRoots[syncRootNode.Id] : null, path);
    }

    public static bool IsNodeOrBranchDeleted<TId, TAltId>(this AdapterTreeNode<TId, TAltId> node)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        // The node with DirtyDeleted flag is a root of deleted branch
        return node.FromNodeToRoot().Any(n => n.Model.Status.HasFlag(AdapterNodeStatus.DirtyDeleted));
    }

    public static IEnumerable<AdapterTreeNode<TId, TAltId>> FromParentToRoot<TId, TAltId>(this AdapterTreeNode<TId, TAltId> node)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return node.FromParentToRoot<AdapterTree<TId, TAltId>, AdapterTreeNode<TId, TAltId>, AdapterTreeNodeModel<TId, TAltId>, TId>();
    }

    public static IEnumerable<AdapterTreeNode<TId, TAltId>> FromNodeToRoot<TId, TAltId>(this AdapterTreeNode<TId, TAltId> node)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return node.FromNodeToRoot<AdapterTree<TId, TAltId>, AdapterTreeNode<TId, TAltId>, AdapterTreeNodeModel<TId, TAltId>, TId>();
    }

    public static RootInfo<TAltId> GetRoot<TId, TAltId>(this AdapterTreeNode<TId, TAltId> node, IReadOnlyDictionary<TId, RootInfo<TAltId>> syncRoots)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return syncRoots[GetSyncRoot(node).Id];
    }

    public static AdapterTreeNode<TId, TAltId> GetSyncRoot<TId, TAltId>(this AdapterTreeNode<TId, TAltId> node)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return FromNodeToRoot(node).SkipLast(1).LastOrDefault() ?? node;
    }

    public static bool IsSyncRoot<TId, TAltId>(this AdapterTreeNode<TId, TAltId> node)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        // First level folders are sync roots, files are not expected on root
        return node.Parent?.IsRoot == true;
    }

    public static int GetVolumeId<TId, TAltId>(this AdapterTreeNode<TId, TAltId> node)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return FromNodeToRoot(node).SkipLast(1).First(n => !n.AltId.IsDefault()).AltId.VolumeId;
    }

    public static NodeInfo<TAltId> ToNodeInfo<TId, TAltId>(this AdapterTreeNode<TId, TAltId> node, IReadOnlyDictionary<TId, RootInfo<TAltId>> syncRoots)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        var (root, path) = node.Path(syncRoots);

        return new NodeInfo<TAltId>()
            .WithId(node.AltId.ItemId)
            .WithParentId(node.Parent != null ? node.Parent.AltId.ItemId : default)
            .WithRoot(root)
            .WithPath(path)
            .WithName(node.Name)
            .WithAttributes(node.Model.Type == NodeType.Directory ? FileAttributes.Directory : default)
            .WithRevisionId(node.Model.RevisionId)
            .WithLastWriteTimeUtc(node.Model.LastWriteTime)
            .WithSize(node.Model.Size);
    }

    private static (AdapterTreeNode<TId, TAltId>? SyncRootNode, string Path) PathWithRoot<TId, TAltId>(AdapterTreeNode<TId, TAltId> node)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        var nodesOnPath = FromRootToNode(node).ToList();

        // First level folders are sync roots
        var syncRootNode = nodesOnPath.FirstOrDefault();
        var path = string.Join(
            System.IO.Path.DirectorySeparatorChar,
            nodesOnPath.Skip(1).Select(n => n.Name));

        return (syncRootNode, path);
    }

    private static IReadOnlyCollection<AdapterTreeNode<TId, TAltId>> FromRootToNode<TId, TAltId>(this AdapterTreeNode<TId, TAltId> node)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return node.FromRootToNode<AdapterTree<TId, TAltId>, AdapterTreeNode<TId, TAltId>, AdapterTreeNodeModel<TId, TAltId>, TId>();
    }
}
