using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Proton.Drive.Shared.Configuration;

namespace Proton.Drive.App.Windows;

internal sealed class AppLifecycleLogger
{
    private const string CurrentVersionKeyName = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    private readonly AppConfig _config;
    private readonly AppArguments _appArguments;
    private readonly ILogger<AppLifecycleLogger> _logger;

    public AppLifecycleLogger(
        AppConfig config,
        AppArguments appArguments,
        ILogger<AppLifecycleLogger> logger)
    {
        _config = config;
        _appArguments = appArguments;
        _logger = logger;
    }

    public void LogAppStart()
    {
        _logger.LogInformation("=========================================================");
        _logger.LogInformation("{AppName} v{AppVersion} ({ProcessArchitecture}) starting", _config.AppName, _config.AppVersion, RuntimeInformation.ProcessArchitecture);
        _logger.LogInformation("OS: {OsVersion}", GetOsVersionString());
        _logger.LogInformation("Launch mode: {LaunchMode}", _appArguments.LaunchMode);
    }

    public void LogAppExit()
    {
        _logger.LogInformation("{AppName} v{AppVersion} ({ProcessArchitecture}) exited", _config.AppName, _config.AppVersion, RuntimeInformation.ProcessArchitecture);
    }

    private static string GetOsVersionString()
    {
        try
        {
            using var currentVersionKey = Registry.LocalMachine.OpenSubKey(CurrentVersionKeyName, writable: false);

            var buildNumber = currentVersionKey?.GetValue("UBR")?.ToString() ?? "0";
            var edition = currentVersionKey?.GetValue("EditionID") as string;
            var displayVersion = currentVersionKey?.GetValue("DisplayVersion") as string;

            return $"{RuntimeInformation.OSDescription}.{buildNumber} ({edition} {displayVersion}) ({RuntimeInformation.OSArchitecture})";
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";
        }
    }
}
