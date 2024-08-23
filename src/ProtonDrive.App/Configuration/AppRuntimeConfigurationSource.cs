using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.App.Configuration;

public sealed class AppRuntimeConfigurationSource : IEnumerable<KeyValuePair<string, string?>>
{
    public static readonly string ProtonFolderName = "Proton";
    public static readonly string ProtonDriveFolderName = "Proton Drive";
    public static readonly string SyncFoldersMappingFilename = "Mappings.json";

    public IEnumerator<KeyValuePair<string, string?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, string?>(nameof(AppConfig.AppVersion), AppVersion());
        yield return new KeyValuePair<string, string?>(nameof(AppConfig.AppFolderPath), AppFolderPath());
        yield return new KeyValuePair<string, string?>(nameof(AppConfig.AppLaunchPath), AppLaunchPath());
        yield return new KeyValuePair<string, string?>(nameof(AppConfig.AppDataPath), AppDataPath());
        yield return new KeyValuePair<string, string?>(nameof(AppConfig.UserDataPath), UserDataPath());

        yield return new KeyValuePair<string, string?>($"Update:{nameof(UpdateConfig.DownloadFolderPath)}", AppUpdatesDownloadPath());
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static string AppVersion()
    {
        // Normalized app version
        return Assembly.GetExecutingAssembly().GetName().Version?.ToNormalized().ToString() ?? string.Empty;
    }

    private static string AppFolderPath()
    {
        // Full path of the folder the app is started from
        return AppContext.BaseDirectory;
    }

    private static string AppLaunchPath()
    {
        // Full path of the app executable
        return Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine app launch path");
    }

    private static string AppDataPath()
    {
        // App specific data folder path
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
            ProtonFolderName,
            ProtonDriveFolderName);

        Directory.CreateDirectory(appDataPath);

        return appDataPath;
    }

    private static string UserDataPath()
    {
        // Windows user specific data folder path
        var userDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.Create),
            ProtonDriveFolderName);

        return userDataPath;
    }

    private static string AppUpdatesDownloadPath()
    {
        // App updates download folder path
        var updatesPath = Path.Combine(AppDataPath(), "Updates");

        Directory.CreateDirectory(updatesPath);

        return updatesPath;
    }
}
