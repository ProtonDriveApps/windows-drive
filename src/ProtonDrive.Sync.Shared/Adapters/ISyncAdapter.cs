using System;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Sync.Shared.Trees.Changes;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Shared.Adapters;

public interface ISyncAdapter<TId> : IFileRevisionProvider<TId>
    where TId : struct, IEquatable<TId>
{
    /// <summary>
    /// A provider of detected updates to be consumed by the Sync Engine.
    /// </summary>
    ITreeChangeProvider<TId> DetectedUpdates { get; }

    /// <summary>
    /// Initializes the adapter, sets file content and mapped node identity provider.
    /// </summary>
    /// <param name="fileRevisionProvider">The file content provider for file
    /// creation and editing operations. It expects the identity value used on another adapter.</param>
    /// <param name="mappedNodeIdProvider">The mapped node identity provider, that provides
    /// a mapped node identity value on other adapter.</param>
    /// <param name="syncedStateProvider">Provides updates to the synced state of the file system tree.</param>
    void Initialize(
        IFileRevisionProvider<TId> fileRevisionProvider,
        IMappedNodeIdentityProvider<TId> mappedNodeIdProvider,
        ITreeChangeProvider<TId> syncedStateProvider);

    /// <summary>
    /// Instructs the adapter to execute the operation on the file system.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>An execution result</returns>
    Task<ExecutionResult<TId>> ExecuteOperation(ExecutableOperation<TId> operation, CancellationToken cancellationToken);
}
