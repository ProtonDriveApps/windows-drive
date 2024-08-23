namespace ProtonDrive.App.Settings;

public enum MappingStatus
{
    /// <summary>
    /// Mapping is new, might be partly setup
    /// </summary>
    New = 0,

    /// <summary>
    /// Mapping is completely setup
    /// </summary>
    Complete = 1,

    /// <summary>
    /// Mapping is deleted by the user, might be partly torn down
    /// </summary>
    Deleted = 2,

    /// <summary>
    /// Mapping is torn down
    /// </summary>
    /// <remarks>
    /// Waiting for the Sync Agent to initialize with latest active mappings,
    /// only then the torn down mapping is completely removed. Mapping names
    /// are used as sync root names in the Sync Agent. Delaying removal of
    /// torn down mappings ensures sync root names are unique in the Sync Agent.
    /// </remarks>
    TornDown = 3,
}
