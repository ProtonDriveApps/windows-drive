using System;
using System.Threading.Tasks;
using ProtonDrive.Update.Config;

namespace ProtonDrive.Update.Updates;

/// <summary>
/// Performs delayed app updates download folder cleanup.
/// </summary>
internal class AppUpdatesAutoCleanup
{
    private readonly IAppUpdates _origin;

    public AppUpdatesAutoCleanup(IAppUpdates origin, AppUpdateConfig config)
    {
        _origin = origin;

        CleanupDelayed(config.CleanupDelay);
    }

    private async void CleanupDelayed(TimeSpan delay)
    {
        await Task.Delay(delay).ConfigureAwait(false);

        _origin.Cleanup();
    }
}
