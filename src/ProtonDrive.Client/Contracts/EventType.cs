namespace ProtonDrive.Client.Contracts;

public enum EventType
{
    /// <summary>
    /// A file or folder was garbage collected or moved out of share view.
    /// </summary>
    Delete = 0,

    /// <summary>
    /// A file or folder was created or moved into a share view. For files, it is
    /// generated when the first revision is committed.
    /// </summary>
    Create = 1,

    /// <summary>
    /// A file content was updated.
    /// </summary>
    Update = 2,

    /// <summary>
    /// A file or folder metadata was updated. Includes updates to name, parent link,
    /// shares, share urls, also to state (stable, trashed, permanently deleted).
    /// </summary>
    UpdateMetadata = 3,
}
