using System;
using System.Threading.Tasks;

namespace ProtonDrive.Update;

public interface INotifyingAppUpdate
{
    /// <summary>
    /// An event raised when the app update state changes.
    /// </summary>
    event EventHandler<IAppUpdateState> StateChanged;

    /// <summary>
    /// Initiate a new check for app update. Status changes are reported by
    /// raising <see cref="StateChanged"/> event.
    /// </summary>
    /// <param name="earlyAccess">Indicates to include Early Access releases</param>
    /// <param name="manual">Indicates whether the request is manual or automatic</param>
    void StartCheckingForUpdate(bool earlyAccess, bool manual = false);

    /// <summary>
    /// Start updating the app to the new release.
    /// </summary>
    /// <param name="auto">Indicates the update is initiated automatically</param>
    /// <remarks>
    /// If update successfully starts, the <see cref="StateChanged"/> event is raised containing
    /// <see cref="IAppUpdateState.Status"/> value <see cref="AppUpdateStatus.Updating"/> and
    /// the app should self-close for the update process to succeed.
    /// </remarks>
    void StartUpdating(bool auto);

    /// <summary>
    /// Try to install downloaded update.
    /// </summary>
    /// <returns>True if installation started, false otherwise.</returns>
    Task<bool> TryInstallDownloadedUpdateAsync();
}
