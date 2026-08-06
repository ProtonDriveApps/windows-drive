using System.IO;
using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Proton.Drive.Shared.Configuration;

namespace Proton.Drive.App.Windows.SystemIntegration;

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
        try
        {
            using var registryKey = Registry.CurrentUser.OpenSubKey(StartupKey, writable: false);

            return registryKey?.GetValue(ProtonDriveRegistryValueName) != null;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            _logger.LogWarning("Impossible to open start-up registry key: {Message}", ex.Message);
            return false;
        }
    }

    public void SetRunApplicationOnStartup(bool value)
    {
        try
        {
            using var registryKey = Registry.CurrentUser.OpenSubKey(StartupKey, writable: true);

            if (registryKey == null)
            {
                // We do not handle the absence of the registry key.
                _logger.LogWarning("Impossible to set the app to open on start-up due to the absence of the registry key");

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
                _logger.LogInformation("App set to open on start-up automatically");
            }
            else if (registryKey.GetValue(ProtonDriveRegistryValueName) != null)
            {
                registryKey.DeleteValue(ProtonDriveRegistryValueName);
                _logger.LogInformation("App disabled to open on start-up automatically");
            }
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            _logger.LogWarning("Impossible to set the app to open on start-up: {Message}", ex.Message);
        }
    }
}
