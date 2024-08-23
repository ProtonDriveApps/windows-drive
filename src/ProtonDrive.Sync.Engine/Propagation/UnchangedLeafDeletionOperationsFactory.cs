using System;
using System.Collections.Generic;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Engine.Propagation;

internal sealed class UnchangedLeafDeletionOperationsFactory<TId>
    where TId : IEquatable<TId>
{
    public IEnumerable<Operation<PropagationTreeNodeModel<TId>>> Operations(PropagationTreeNode<TId> node)
    {
        if (!node.IsRoot
            && node.Model.LocalStatus == UpdateStatus.Unchanged
            && node.Model.RemoteStatus == UpdateStatus.Unchanged
            && node.IsLeaf)
        {
            yield return new Operation<PropagationTreeNodeModel<TId>>(
                OperationType.Delete,
                node.Model.Copy());
        }
    }
}
