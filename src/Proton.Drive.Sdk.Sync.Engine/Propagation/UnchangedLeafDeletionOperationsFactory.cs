using Proton.Drive.Sdk.Sync.Engine.Shared;
using Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Propagation;
using Proton.Drive.Sdk.Sync.Shared.Trees.Operations;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Engine.Propagation;

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
