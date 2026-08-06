using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.ExecutionStatistics;
using Proton.Drive.Sdk.Sync.Shared.SyncActivity;

namespace Proton.Drive.Sdk.Sync.Adapter;

public interface IManagedAdapter<TId> : IExecutionStatisticsProvider
{
    /// <summary>
    /// Raised when synchronization activity item changes.
    /// </summary>
    event EventHandler<SyncActivityChangedEventArgs<TId>> SyncActivityChanged;

    /// <summary>
    /// Indicates whether the adapter contains dirty nodes to enumerate.
    /// </summary>
    bool HasUpdatesToDetect { get; }

    /// <summary>
    /// Indicates whether the adapter contains detected updates not yet consumed by
    /// the Sync Engine.
    /// </summary>
    bool HasUpdatesToSynchronize { get; }

    /// <summary>
    /// An intermittent transacted scheduler used by the adapter for synchronizing access
    /// to the internal state and adapter database.
    /// </summary>
    ITransactedScheduler SyncScheduler { get; }

    /// <summary>
    /// Starts automatic file system event-based update detection and retrieves attributes
    /// of the replica root node.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous connect operation.</returns>
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops automatic event-based update detection.
    /// </summary>
    /// <returns>A task that represents the asynchronous disconnect operation.</returns>
    Task DisconnectAsync();

    /// <summary>
    /// Runs state-based update detection.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous update detection operation.</returns>
    Task DetectUpdatesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Triggers a pseudo edit for the specified file, simulating a content change.
    /// </summary>
    /// <param name="id">The file node ID to trigger pseudo edit for.</param>
    /// <param name="requestedVersion">Expected file content version.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task TriggerPseudoFileEditAsync(TId id, long requestedVersion, CancellationToken cancellationToken);
}
