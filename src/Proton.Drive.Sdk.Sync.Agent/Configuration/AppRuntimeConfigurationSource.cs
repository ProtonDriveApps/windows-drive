using System.Collections;
using System.Reflection;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Agent.Configuration;

public sealed class AppRuntimeConfigurationSource : IEnumerable<KeyValuePair<string, string?>>
{
    public static readonly string ProtonFolderName = "Proton";
    public static readonly string ProtonDriveFolderName = "Proton Drive";
    public static readonly string AppUpdatesFolderName = "Updates";
    public static readonly string WebView2DataFolderName = "WebView2";
    public static readonly string SyncFoldersMappingFilename = "Mappings.json";

    public IEnumerator<KeyValuePair<string, string?>> GetEnumerator()
    {
        yield return new KeyValuePair<string, string?>(nameof(AppConfig.AppVersion), GetAppVersion());
        yield return new KeyValuePair<string, string?>(nameof(AppConfig.AppFolderPath), GetAppFolderPath());
        yield return new KeyValuePair<string, string?>(nameof(AppConfig.AppLaunchPath), GetAppLaunchPath());
        yield return new KeyValuePair<string, string?>(nameof(AppConfig.AppDataPath), GetAppDataPath());
        yield return new KeyValuePair<string, string?>(nameof(AppConfig.UserDataPath), GetUserDataPath());
        yield return new KeyValuePair<string, string?>(nameof(AppConfig.WebView2DataPath), GetWebView2DataPath());

        yield return new KeyValuePair<string, string?>($"Update:{nameof(UpdateConfig.DownloadFolderPath)}", GetAppUpdatesDownloadPath());
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static string GetAppVersion()
    {
        // Normalized app version
        return Assembly.GetExecutingAssembly().GetName().Version?.ToNormalized().ToString() ?? string.Empty;
    }

    private static string GetAppFolderPath()
    {
        // Full path of the folder the app is started from
        return AppContext.BaseDirectory;
    }

    private static string GetAppLaunchPath()
    {
        // Full path of the app executable
        return Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine app launch path");
    }

    private static string GetAppDataPath()
    {
        // App specific data folder path
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
            ProtonFolderName,
            ProtonDriveFolderName);

        Directory.CreateDirectory(appDataPath);

        return appDataPath;
    }

    private static string GetUserDataPath()
    {
        // Windows user specific data folder path
        var userDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.Create),
            ProtonDriveFolderName);

        return userDataPath;
    }

    private static string GetWebView2DataPath()
    {
        // WebView2 cache data folder path
        var webView2DataPath = Path.Combine(GetAppDataPath(), WebView2DataFolderName);

        Directory.CreateDirectory(webView2DataPath);

        return webView2DataPath;
    }

    private static string GetAppUpdatesDownloadPath()
    {
        // App updates download folder path
        var updatesPath = Path.Combine(GetAppDataPath(), AppUpdatesFolderName);

        Directory.CreateDirectory(updatesPath);

        return updatesPath;
    }
}
