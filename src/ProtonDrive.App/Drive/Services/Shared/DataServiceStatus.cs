namespace ProtonDrive.App.Drive.Services.Shared;

public enum DataServiceStatus
{
    /// <summary>
    /// Waiting to be requested to retrieve data for the first time
    /// or become ready for automatic data retrieval.
    /// </summary>
    Idle,

    /// <summary>
    /// Performing data retrieval.
    /// </summary>
    LoadingData,

    /// <summary>
    /// Data retrieval has succeeded.
    /// </summary>
    Succeeded,

    /// <summary>
    /// Data retrieval has failed.
    /// </summary>
    Failed,
}
