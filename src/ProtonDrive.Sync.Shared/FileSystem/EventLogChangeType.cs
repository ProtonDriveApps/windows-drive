namespace ProtonDrive.Sync.Shared.FileSystem;

public enum EventLogChangeType
{
    /// <summary>
    /// File system object has been created or moved into the replica branch from outside the branch.
    /// If a directory has been moved, it might contain children.
    /// </summary>
    Created,

    /// <summary>
    /// File system object has been created, moved into the replica branch from outside the branch,
    /// or moved within the replica branch. If moved withing the replica branch, the preceding
    /// <see cref="DeletedOrMovedFrom"/> event log entry describes the old parent. If a directory has
    /// been moved, it might contain children.
    /// </summary>
    CreatedOrMovedTo,

    /// <summary>
    /// File system object metadata has changed.
    /// </summary>
    Changed,

    /// <summary>
    /// File system object metadata has changed and/or file system object has been renamed and/or
    /// moved to a different parent.
    /// </summary>
    ChangedOrMoved,

    /// <summary>
    /// File system object has been renamed and/or moved to a different parent.
    /// </summary>
    Moved,

    /// <summary>
    /// File system object has been deleted or moved from replica branch to outside of the branch.
    /// </summary>
    Deleted,

    /// <summary>
    /// File system object has been deleted, moved from replica branch to outside of the branch,
    /// or moved within the replica branch. If moved withing the replica branch, the subsequent
    /// <see cref="CreatedOrMovedTo"/> event log entry describes the new parent.
    /// </summary>
    DeletedOrMovedFrom,

    /// <summary>
    /// Some event log entries were skipped.
    /// </summary>
    Skipped,

    /// <summary>
    /// Event log entry retrieval has failed.
    /// </summary>
    Error,
}
