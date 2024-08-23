namespace ProtonDrive.Shared.Telemetry;

public enum ErrorScope
{
    /// <summary>
    /// Error related to the sync pass
    /// </summary>
    Sync,

    /// <summary>
    /// Error related to the synchronisation of the item
    /// </summary>
    ItemOperation,

    /// <summary>
    /// Error occurred when attempting to open a .protondoc file
    /// </summary>
    DocumentOpening,

    /// <summary>
    /// Error occurred when attempting to sanitize a .protondoc file by adding the file extension
    /// </summary>
    DocumentNameMigration,
}
