using System;
using ProtonDrive.Sync.Adapter.Trees.Adapter.NodeLinking;

namespace ProtonDrive.Sync.Adapter.Trees.Adapter;

[Flags]
public enum AdapterNodeStatus
{
    None = 0,

    /// <summary>
    /// The new file or directory has been created in the file system or an unknown one was
    /// moved from the dirty branch. Lacks correct Type and Attributes, requires enumeration.
    /// </summary>
    /// <remarks>
    /// Always a Directory and has DirtyAttributes flag, cannot contain children.
    /// Node cannot be the target of the operations executed by the Sync Engine.
    /// Not reported to the Sync Engine to prevent inconsistencies.
    /// </remarks>
    DirtyPlaceholder = 1,

    /// <summary>
    /// The attributes have been changed in the file system and require enumeration.
    /// </summary>
    /// <remarks>
    /// The node cannot be the target of the operations executed by the Sync Engine.
    /// </remarks>
    DirtyAttributes = 2,

    /// <summary>
    /// The parent has been changed in the file system but is not yet known.
    /// </summary>
    /// <remarks>
    /// <para>Is invisible to the node lookup by path.</para>
    /// <para>The node cannot be the target of the operations executed by the Sync Engine.</para>
    /// <para>Not reported to the Sync Engine to prevent inconsistencies.</para>
    /// </remarks>
    DirtyParent = 4,

    /// <summary>
    /// The file or directory has been deleted from the file system while the tree branch is dirty.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cannot be removed while the tree branch starting at this node is dirty to prevent accidentally
    /// removing dirty nodes which were moved to different branch in the file system before deletion.
    /// </para>
    /// <para>Is invisible to the node lookup by path.</para>
    /// <para>The node and descendants cannot be the target of the operations executed by the Sync Engine.</para>
    /// <para>Not reported to the Sync Engine to prevent inconsistencies.</para>
    /// </remarks>
    DirtyDeleted = 8,

    /// <summary>
    /// The directory might contain new children and requires enumeration.
    /// </summary>
    /// <remarks>
    /// The current children are assumed not dirty, so the node does not make the branch dirty.
    /// </remarks>
    DirtyChildren = 16,

    /// <summary>
    /// The directory contains dirty descendants and requires deep enumeration.
    /// </summary>
    DirtyDescendants = 32 | DirtyChildren,

    /// <summary>
    /// A placeholder of the source node copied to a different move scope due to move was replaced with
    /// copying and deletion of the source node.
    /// </summary>
    /// <remarks>
    /// Does not have AltId value.
    /// Either the node or an ancestor has the <see cref="AdapterNodeStatus.DirtyDeleted"/> flag set.
    /// Prevents branch from being deleted.
    /// The node participates as source in the node link of type <see cref="NodeLinkType.Copied"/>.
    /// </remarks>
    DirtyCopiedFrom = 1 << 6,

    /// <summary>
    /// The destination node copied from a different move scope due to move was replaced with
    /// copying and deletion of the source node.
    /// </summary>
    /// <remarks>
    /// The node participates as destination in the node link of type <see cref="NodeLinkType.Copied"/>.
    /// </remarks>
    DirtyCopiedTo = 1 << 7,

    DirtyNodeMask = DirtyPlaceholder | DirtyAttributes | DirtyParent | DirtyDeleted,
    DirtyMask = DirtyNodeMask | DirtyChildren | DirtyDescendants | DirtyCopiedFrom | DirtyCopiedTo,

    /// <summary>
    /// The Adapter Tree node state matches the synced state (the Synced Tree node state in the Sync Engine).
    /// </summary>
    Synced = 1 << 16,

    /// <summary>
    /// File or folder state update is pending on the on-demand hydration file system.
    /// </summary>
    /// <remarks>
    /// There are three cases when the file or folder state update is required:
    /// <list type="bullet">
    /// <item>The placeholder state is not in-sync</item>
    /// <item>The file is pinned (marked to be available offline) but not hydrated</item>
    /// <item>The file is unpinned (marked to free space) but fully or partly hydrated</item>
    /// </list>
    /// </remarks>
    StateUpdatePending = 1 << 17,

    /// <summary>
    /// File hydration is pending on the on-demand hydration file system.
    /// </summary>
    /// <remarks>
    /// The file is pinned (marked to be available offline) but not fully hydrated.
    /// </remarks>
    HydrationPending = 1 << 18,

    StateUpdateFlagsMask = StateUpdatePending | HydrationPending,
}
