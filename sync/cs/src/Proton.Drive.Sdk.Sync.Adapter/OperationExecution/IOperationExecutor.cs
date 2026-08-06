using Proton.Drive.Sdk.Sync.Shared.Adapters;
using Proton.Drive.Sdk.Sync.Shared.Trees.Operations;

namespace Proton.Drive.Sdk.Sync.Adapter.OperationExecution;

internal interface IOperationExecutor<TId>
    where TId : struct, IEquatable<TId>
{
    Task<ExecutionResult<TId>> ExecuteAsync(ExecutableOperation<TId> operation, CancellationToken cancellationToken);
}
