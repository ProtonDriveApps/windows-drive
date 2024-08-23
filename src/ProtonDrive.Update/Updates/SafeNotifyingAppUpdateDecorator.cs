using System;
using System.Threading.Tasks;

namespace ProtonDrive.Update.Updates;

/// <summary>
/// Suppresses expected exceptions of <see cref="NotifyingAppUpdate.StartUpdating"/> method.
/// </summary>
public class SafeNotifyingAppUpdateDecorator : INotifyingAppUpdate
{
    private readonly INotifyingAppUpdate _origin;

    public SafeNotifyingAppUpdateDecorator(INotifyingAppUpdate origin)
    {
        _origin = origin;
    }

    public event EventHandler<IAppUpdateState> StateChanged
    {
        add => _origin.StateChanged += value;
        remove => _origin.StateChanged -= value;
    }

    public void StartCheckingForUpdate(bool earlyAccess, bool manual = false) => _origin.StartCheckingForUpdate(earlyAccess, manual);

    public void StartUpdating(bool auto)
    {
        try
        {
            _origin.StartUpdating(auto);
        }
        catch (AppUpdateException)
        {
            // Suppress expected exceptions
        }
    }

    public async Task<bool> TryInstallDownloadedUpdateAsync()
    {
        try
        {
            return await _origin.TryInstallDownloadedUpdateAsync().ConfigureAwait(false);
        }
        catch (AppUpdateException)
        {
            // Suppress expected exceptions
            return false;
        }
    }
}
