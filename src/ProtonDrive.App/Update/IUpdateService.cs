using System;
using System.Threading.Tasks;
using ProtonDrive.Update;

namespace ProtonDrive.App.Update;

public interface IUpdateService
{
    /// <summary>
    /// An event raised when the update service state changes.
    /// </summary>
    event EventHandler<UpdateState> StateChanged;

    /// <summary>
    /// Initiate a new check for app update. Status changes are reported by
    /// raising <see cref="StateChanged"/> event.
    /// </summary>
    void StartCheckingForUpdate();

    /// <summary>
    /// Start updating the app to the new release.
    /// </summary>
    /// <remarks>
    /// If update successfully starts, the <see cref="StateChanged"/> event is raised containing
    /// <see cref="IAppUpdateState.Status"/> value <see cref="AppUpdateStatus.Updating"/> and
    /// the app should self-close for the update process to succeed.
    /// </remarks>
    void StartUpdating();

    /// <summary>
    /// Try installing a downloaded update.
    /// </summary>
    /// <returns>True if a downloaded update was available, false otherwise.</returns>
    Task<bool> TryInstallDownloadedUpdateAsync();
}
