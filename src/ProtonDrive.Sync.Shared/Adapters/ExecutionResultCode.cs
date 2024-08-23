namespace ProtonDrive.Sync.Shared.Adapters;

public enum ExecutionResultCode
{
    Success,

    /// <summary>
    /// Other node with the duplicate name exists
    /// </summary>
    NameConflict,

    /// <summary>
    /// Cannot execute operation on this node
    /// </summary>
    DirtyNode,

    /// <summary>
    /// Cannot execute operation on this branch
    /// </summary>
    DirtyBranch,

    /// <summary>
    /// Cannot move to this folder
    /// </summary>
    DirtyDestination,

    /// <summary>
    /// Operation was cancelled
    /// </summary>
    Cancelled,

    /// <summary>
    /// The replica is offline.
    /// This value is not returned if the source replica in the move operation is offline,
    /// because operations are executed on the destination replica.
    /// </summary>
    Offline,

    /// <summary>
    /// The operation was skipped internally.
    /// For internal use in the SyncEngine only.
    /// </summary>
    SkippedInternally,

    /// <summary>
    /// Execution skipped due to file system access retry rate limiting.
    /// </summary>
    RetryRateLimitExceeded,

    /// <summary>
    /// Execution skipped due to file system access rate limiting.
    /// </summary>
    AccessRateLimitExceeded,

    // Other error
    Error,
}
