using System;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Shared.Adapters;

namespace ProtonDrive.Sync.Engine.Propagation;

internal interface INodePropagationPipeline<TId>
    where TId : IEquatable<TId>
{
    Task<ExecutionResultCode> ExecuteAsync(
        PropagationTreeNode<TId> node,
        Func<PropagationTreeNodeModel<TId>, UpdateStatus, UpdateStatus> statusFilter,
        CancellationToken cancellationToken);
}
