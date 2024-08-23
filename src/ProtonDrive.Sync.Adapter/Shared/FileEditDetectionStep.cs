using System;
using System.Linq;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Adapter.UpdateDetection;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.Shared;

internal sealed class FileEditDetectionStep<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    public AdapterTreeNode<TId, TAltId>? Execute(
        AdapterTreeNode<TId, TAltId>? node,
        IncomingAdapterTreeNodeModel<TId, TAltId> incomingNodeModel,
        AdapterTreeNode<TId, TAltId> parentNode)
    {
        if (node != null)
        {
            // Not creation of a new item
            return node;
        }

        if (incomingNodeModel.Type is not NodeType.File)
        {
            // Not a file
            return node;
        }

        if (parentNode.Model.Status.HasFlag(AdapterNodeStatus.DirtyDeleted))
        {
            // The parent is marked as deleted
            return node;
        }

        // Direct children of a folder, that is being enumerated, have DirtyAttributes flag.
        // File renamed to a temporary name before overwriting, has DirtyDeleted flag.
        // File deleted on the file system, before it is detected as really deleted, has DirtyParent flag.
        var editedFileCandidates = parentNode
            .ChildrenByName(incomingNodeModel.Name)
            .Where(
                n => n.Type is NodeType.File
                     && !n.Model.Status.HasFlag(AdapterNodeStatus.DirtyPlaceholder)
                     && n.Model.Status.HasAnyFlag(AdapterNodeStatus.DirtyAttributes | AdapterNodeStatus.DirtyParent | AdapterNodeStatus.DirtyDeleted))
            .ToList();

        if (editedFileCandidates.Count != 1)
        {
            // There is no or more than one candidate for file edit detection
            return node;
        }

        return editedFileCandidates[0];
    }
}
