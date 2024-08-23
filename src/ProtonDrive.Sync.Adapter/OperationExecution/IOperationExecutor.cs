using System;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Sync.Shared.Adapters;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.OperationExecution;

internal interface IOperationExecutor<TId>
    where TId : struct, IEquatable<TId>
{
    Task<ExecutionResult<TId>> ExecuteAsync(ExecutableOperation<TId> operation, CancellationToken cancellationToken);
}
