namespace ProtonDrive.Update;

public enum AppUpdateStatus
{
    /// <summary>
    /// No update is available
    /// </summary>
    None,

    /// <summary>
    /// Checking for an update
    /// </summary>
    Checking,

    /// <summary>
    /// Checking for an update has failed
    /// </summary>
    CheckFailed,

    /// <summary>
    /// An update is available and is being downloaded
    /// </summary>
    Downloading,

    /// <summary>
    /// Downloading an update has failed
    /// </summary>
    DownloadFailed,

    /// <summary>
    /// An update is downloaded and ready to update
    /// </summary>
    Ready,

    /// <summary>
    /// The app updating has started, the app should self-close
    /// </summary>
    Updating,
}
