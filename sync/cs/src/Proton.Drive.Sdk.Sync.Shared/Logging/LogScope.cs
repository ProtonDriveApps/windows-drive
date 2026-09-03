namespace Proton.Drive.Sdk.Sync.Shared.Logging;

public static class LogScope
{
    /// <summary>
    /// Update detection pass: both adapters are asked to detect changes on their replica.
    /// </summary>
    public const string UpdateDetection = "UPD";

    /// <summary>
    /// Event log-based update detection: changes are derived from the replica's event log.
    /// </summary>
    public const string LogBasedUpdateDetection = "LGB";

    /// <summary>
    /// State-based update detection: changes are derived by enumerating dirty nodes.
    /// </summary>
    public const string StateBasedUpdateDetection = "STB";

    /// <summary>
    /// Synchronization pass: the Sync Engine reconciles and propagates detected changes.
    /// </summary>
    public const string Synchronization = "SYN";

    /// <summary>
    /// Consolidation: detected updates are merged into the Sync Engine trees, for both replicas.
    /// </summary>
    public const string Consolidation = "CON";

    /// <summary>
    /// Reconciliation: divergences between the two replicas are resolved into sync operations.
    /// </summary>
    public const string Reconciliation = "REC";

    /// <summary>
    /// Propagation: reconciled operations are executed against the replicas.
    /// </summary>
    public const string Propagation = "PRP";

    /// <summary>
    /// File sync state maintenance: in-sync marking, hydration and dehydration of placeholders, driven by status flags.
    /// </summary>
    public const string SyncStateMaintenance = "SSM";
}
