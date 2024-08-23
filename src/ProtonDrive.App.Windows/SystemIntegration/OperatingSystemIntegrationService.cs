using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using ProtonDrive.Shared.Configuration;

namespace ProtonDrive.App.Windows.SystemIntegration;

internal sealed class OperatingSystemIntegrationService : IOperatingSystemIntegrationService
{
    private const string StartupKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string ProtonDriveRegistryValueName = "Proton Drive";

    private readonly AppConfig _appConfig;
    private readonly ILogger<OperatingSystemIntegrationService> _logger;

    public OperatingSystemIntegrationService(AppConfig appConfig, ILogger<OperatingSystemIntegrationService> logger)
    {
        _appConfig = appConfig;
        _logger = logger;
    }

    public bool GetRunApplicationOnStartup()
    {
        var registryKey = Registry.CurrentUser.OpenSubKey(StartupKey, true);

        return registryKey?.GetValue(ProtonDriveRegistryValueName) != null;
    }

    public void SetRunApplicationOnStartup(bool value)
    {
        var registryKey = Registry.CurrentUser.OpenSubKey(StartupKey, true);

        if (registryKey == null)
        {
            // We do not handle the absence of the the registry key.
            _logger.LogWarning("Impossible to set the app to open on start-up due to the absence of the registry key.");

            return;
        }

        if (value)
        {
            if (registryKey.GetValue(ProtonDriveRegistryValueName) != null)
            {
                // Registry key value already set.
                return;
            }

            registryKey.SetValue(ProtonDriveRegistryValueName, $"\"{_appConfig.AppLaunchPath}\" -quiet");
            _logger.LogDebug("App set to open on start-up automatically.");
        }
        else if (registryKey.GetValue(ProtonDriveRegistryValueName) != null)
        {
            registryKey.DeleteValue(ProtonDriveRegistryValueName);
            _logger.LogDebug("App disabled to open on start-up automatically.");
        }
    }
}
