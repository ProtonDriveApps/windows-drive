using System;
using System.Collections.Generic;
using System.Linq;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.UpdateDetection.StateBased.Enumeration;

internal class ChildrenEnumerationPreparationStep<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly NodeUpdateDetection<TId, TAltId> _nodeUpdateDetection;

    public ChildrenEnumerationPreparationStep(NodeUpdateDetection<TId, TAltId> nodeUpdateDetection)
    {
        _nodeUpdateDetection = nodeUpdateDetection;
    }

    public IDictionary<TId, AdapterTreeNode<TId, TAltId>> Execute(AdapterTreeNode<TId, TAltId> node)
    {
        bool parentHasDirtyDescendants = node.Model.HasDirtyDescendantsFlag();

        var children = node.Children
            // Cannot do anything with the children known to be deleted.
            .Where(n => !n.Model.Status.HasFlag(AdapterNodeStatus.DirtyDeleted))
            .ToDictionary(n => n.Id);

        foreach (var child in children.Values)
        {
            // All cached children get DirtyAttributes flag.
            // If the child gets concurrently updated by the log-based update detection,
            // it looses DirtyAttributes flag.
            var model = IncomingAdapterTreeNodeModel<TId, TAltId>
                .FromNodeModel(child.Model)
                .WithAppendedDirtyFlags(AdapterNodeStatus.DirtyAttributes);

            if (parentHasDirtyDescendants && child.Type == NodeType.Directory)
            {
                // Cached child directories get DirtyDescendants flag
                model = model
                    .WithAppendedDirtyFlags(AdapterNodeStatus.DirtyDescendants);
            }

            DetectNodeUpdate(child, model);
        }

        return children;
    }

    private void DetectNodeUpdate(AdapterTreeNode<TId, TAltId>? current, IncomingAdapterTreeNodeModel<TId, TAltId>? incoming)
    {
        _nodeUpdateDetection.Execute(current, incoming);
    }
}
