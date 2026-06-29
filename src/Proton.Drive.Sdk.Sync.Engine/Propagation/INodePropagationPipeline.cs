using Proton.Drive.Sdk.Sync.Engine.Shared;
using Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Propagation;
using Proton.Drive.Sdk.Sync.Shared.Adapters;

namespace Proton.Drive.Sdk.Sync.Engine.Propagation;

internal interface INodePropagationPipeline<TId>
    where TId : IEquatable<TId>
{
    Task<ExecutionResultCode> ExecuteAsync(
        PropagationTreeNode<TId> node,
        Func<PropagationTreeNodeModel<TId>, UpdateStatus, UpdateStatus> statusFilter,
        CancellationToken cancellationToken);
}
