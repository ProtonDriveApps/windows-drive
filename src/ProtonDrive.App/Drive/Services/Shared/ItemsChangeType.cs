namespace ProtonDrive.App.Drive.Services.Shared;

public enum ItemsChangeType
{
    /// <summary>
    /// A new item added to the data set.
    /// </summary>
    Added,

    /// <summary>
    /// The data set item successfully updated.
    /// </summary>
    Updated,

    /// <summary>
    /// The data set item successfully removed.
    /// </summary>
    Removed,

    /// <summary>
    /// All items removed from the data set.
    /// </summary>
    Cleared,

    /// <summary>
    /// Attempted to update the item that did not exist in the data set.
    /// </summary>
    AttemptedToUpdate,

    /// <summary>
    /// Attempted to remove the item that did not exist in the data set.
    /// </summary>
    AttemptedToRemove,
}
