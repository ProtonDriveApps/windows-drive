using System;
using System.Collections.Generic;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.UpdateDetection.StateBased.Enumeration;

internal class ChildrenEnumerationCompletionStep<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly NodeUpdateDetection<TId, TAltId> _nodeUpdateDetection;

    public ChildrenEnumerationCompletionStep(NodeUpdateDetection<TId, TAltId> nodeUpdateDetection)
    {
        _nodeUpdateDetection = nodeUpdateDetection;
    }

    public void Execute(
        AdapterTreeNode<TId, TAltId> node,
        IDictionary<TId, AdapterTreeNode<TId, TAltId>> unprocessedChildren)
    {
        EscapeIfDeleted(node);

        bool parentHasDirtyDescendants = node.Model.HasDirtyDescendantsFlag();

        foreach (var child in unprocessedChildren.Values)
        {
            if (child.IsDeleted || !child.Model.ParentId.Equals(node.Id))
            {
                // The child has been concurrently deleted or moved to another parent, skipping.
                continue;
            }

            if (child.Model.IsDirtyPlaceholder())
            {
                // Unprocessed placeholders (that were not found during enumeration) are deleted.
                DetectNodeUpdate(child, null);

                continue;
            }

            // If the child gets concurrently updated by the log-based update detection,
            // it looses DirtyAttributes flag.
            if (!child.Model.Status.HasAnyFlag(AdapterNodeStatus.DirtyAttributes))
            {
                continue;
            }

            // Other unprocessed children (that were not found during enumeration) get DirtyParent flag
            var childModel = IncomingAdapterTreeNodeModel<TId, TAltId>
                .FromNodeModel(child.Model)
                .WithAppendedDirtyFlags(AdapterNodeStatus.DirtyParent);

            if (parentHasDirtyDescendants && child.Type == NodeType.Directory)
            {
                childModel = childModel
                    .WithAppendedDirtyFlags(AdapterNodeStatus.DirtyDescendants);
            }

            DetectNodeUpdate(child, childModel);
        }

        var incoming = IncomingAdapterTreeNodeModel<TId, TAltId>
            .FromNodeModel(node.Model)
            .WithRemovedDirtyFlags(AdapterNodeStatus.DirtyDescendants);

        DetectNodeUpdate(node, incoming);
    }

    private void EscapeIfDeleted(AdapterTreeNode<TId, TAltId> node)
    {
        if (node.IsDeleted)
        {
            Escape();
        }
    }

    private void Escape()
    {
        throw new EscapeException();
    }

    private void DetectNodeUpdate(AdapterTreeNode<TId, TAltId>? current, IncomingAdapterTreeNodeModel<TId, TAltId>? incoming)
    {
        _nodeUpdateDetection.Execute(current, incoming);
    }
}
