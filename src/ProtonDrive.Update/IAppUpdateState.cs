using System.Collections.Generic;

namespace ProtonDrive.Update;

/// <summary>
/// Public app update state
/// </summary>
public interface IAppUpdateState
{
    /// <summary>
    /// The list of latest app releases
    /// </summary>
    IReadOnlyList<IRelease> ReleaseHistory { get; }

    /// <summary>
    /// Indicates the new app release is available for download
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Indicates the new app release is downloaded and ready to update
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// The update status value
    /// </summary>
    AppUpdateStatus Status { get; }
}
