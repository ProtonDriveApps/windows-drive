using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.Update.Updates;

public class LoggingNotifyingAppUpdateDecorator : INotifyingAppUpdate
{
    private readonly ILogger<LoggingNotifyingAppUpdateDecorator> _logger;
    private readonly INotifyingAppUpdate _origin;

    public LoggingNotifyingAppUpdateDecorator(ILogger<LoggingNotifyingAppUpdateDecorator> logger, INotifyingAppUpdate origin)
    {
        _logger = logger;
        _origin = origin;

        _origin.StateChanged += OriginOnStateChanged;
    }

    public event EventHandler<IAppUpdateState>? StateChanged;

    public void StartCheckingForUpdate(bool earlyAccess, bool manual = false)
    {
        _logger.LogInformation("Requested to start checking for an app update, EarlyAccess={EarlyAccess}, Manual={Manual}", earlyAccess, manual);

        _origin.StartCheckingForUpdate(earlyAccess, manual);
    }

    public void StartUpdating(bool auto)
    {
        try
        {
            _logger.LogInformation("Requested to start updating the app, Auto={Auto}", auto);

            _origin.StartUpdating(auto);
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to start updating the app: {Error}", e.CombinedMessage());

            throw;
        }
    }

    public async Task<bool> TryInstallDownloadedUpdateAsync()
    {
        _logger.LogInformation("Requested to try to install downloaded update");

        var updateInstallationStarted = await _origin.TryInstallDownloadedUpdateAsync().ConfigureAwait(false);

        _logger.LogInformation(updateInstallationStarted ? "Downloaded update was available and installation started" : "No downloaded update to install");

        return updateInstallationStarted;
    }

    private void OriginOnStateChanged(object? sender, IAppUpdateState state)
    {
        _logger.LogInformation(
            "App update state changed to {Status} (available={IsAvailable}, ready={IsReady})",
            state.Status,
            state.IsAvailable,
            state.IsReady);

        OnStateChanged(state);
    }

    private void OnStateChanged(IAppUpdateState state)
    {
        StateChanged?.Invoke(this, state);
    }
}
