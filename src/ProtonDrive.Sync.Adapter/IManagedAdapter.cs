using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.ExecutionStatistics;
using ProtonDrive.Sync.Shared.SyncActivity;

namespace ProtonDrive.Sync.Adapter;

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
}
