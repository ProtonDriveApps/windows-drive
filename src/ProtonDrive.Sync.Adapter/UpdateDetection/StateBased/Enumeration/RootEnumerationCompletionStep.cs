using System;
using System.Collections.Generic;
using ProtonDrive.Sync.Adapter.Trees.Adapter;

namespace ProtonDrive.Sync.Adapter.UpdateDetection.StateBased.Enumeration;

internal sealed class RootEnumerationCompletionStep<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly NodeUpdateDetection<TId, TAltId> _nodeUpdateDetection;

    public RootEnumerationCompletionStep(NodeUpdateDetection<TId, TAltId> nodeUpdateDetection)
    {
        _nodeUpdateDetection = nodeUpdateDetection;
    }

    public void Execute(
        AdapterTreeNode<TId, TAltId> rootNode,
        IDictionary<TId, AdapterTreeNode<TId, TAltId>> unprocessedChildren)
    {
        foreach (var child in unprocessedChildren.Values)
        {
            if (child.IsDeleted || !child.Model.ParentId.Equals(rootNode.Id))
            {
                // The child has been concurrently deleted or moved to another parent, skipping.
                continue;
            }

            // Sync root (first level folder) is immediately deleted regardless of its state
            // or descendants state.
            DetectNodeUpdate(child, null);
        }
    }

    private void DetectNodeUpdate(
        AdapterTreeNode<TId, TAltId>? currentNode,
        IncomingAdapterTreeNodeModel<TId, TAltId>? incomingNodeModel)
    {
        _nodeUpdateDetection.Execute(currentNode, incomingNodeModel);
    }
}
