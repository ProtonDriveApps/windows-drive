using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Logging;

namespace ProtonDrive.App.Windows.SystemIntegration;

internal sealed class Win32ShellSyncFolderRegistry : IShellSyncFolderRegistry
{
    private const string ProtonDriveGuid = "{17EB1F07-4A59-4881-9BCB-9982E4A89547}";
    private const string ShellFileSystemFolderGuid = "{0E5AAE11-A475-4c5b-AB00-C66DE400274E}";
    private const string ClassIdKey = "Software\\Classes\\CLSID";
    private const string ExplorerKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer";

    private readonly AppConfig _appConfig;
    private readonly ILogger<Win32ShellSyncFolderRegistry> _logger;

    public Win32ShellSyncFolderRegistry(AppConfig appConfig, ILogger<Win32ShellSyncFolderRegistry> logger)
    {
        _appConfig = appConfig;
        _logger = logger;
    }

    public static void Unregister()
    {
        Safe(TryUnregister);
    }

    void IShellSyncFolderRegistry.Unregister()
    {
        Safe(() => WithLoggedException(TryUnregister));
    }

    public void Register(string path)
    {
        if (!Safe(() => WithLoggedException(() => TryRegister(path))))
        {
            Safe(TryUnregister);
        }
    }

    private static RegistryKey GetOrCreateSubKey(RegistryKey registryKey, string subKey)
    {
        return registryKey.CreateSubKey(subKey, true)
               ?? throw new InvalidOperationException($"Registry key '{registryKey.Name}\\{subKey}' not found.");
    }

    private static bool Safe(Action action)
    {
        try
        {
            action.Invoke();

            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException
                                       or SystemException
                                       or UnauthorizedAccessException
                                       or IOException
                                       or ObjectDisposedException)
        {
            return false;
        }
    }

    private static void TryUnregister()
    {
        var newStartPanelKey = Registry.CurrentUser.OpenSubKey($"{ExplorerKey}\\HideDesktopIcons\\NewStartPanel", true);

        newStartPanelKey?.DeleteValue(ProtonDriveGuid, false);

        var classIdRegistryKey = Registry.CurrentUser.OpenSubKey(ClassIdKey, true);

        classIdRegistryKey?.DeleteSubKeyTree(ProtonDriveGuid, false);

        var nameSpaceKey = Registry.CurrentUser.OpenSubKey($"{ExplorerKey}\\Desktop\\NameSpace", true);

        nameSpaceKey?.DeleteSubKeyTree(ProtonDriveGuid, false);
    }

    private void TryRegister(string path)
    {
        var classIdRegistryKey = Registry.CurrentUser.OpenSubKey(ClassIdKey, true);

        if (classIdRegistryKey == null)
        {
            throw new InvalidOperationException($"Registry key '{ClassIdKey}' not found.");
        }

        var protonDriveClassIdRegistryKey = GetOrCreateSubKey(classIdRegistryKey, ProtonDriveGuid);

        protonDriveClassIdRegistryKey.SetValue(default, "Proton Drive", RegistryValueKind.String);

        protonDriveClassIdRegistryKey.SetValue("System.IsPinnedToNameSpaceTree", 0x1, RegistryValueKind.DWord);

        protonDriveClassIdRegistryKey.SetValue("SortOrderIndex", 0x42, RegistryValueKind.DWord);

        var defaultIconKey = GetOrCreateSubKey(protonDriveClassIdRegistryKey, "DefaultIcon");

        defaultIconKey.SetValue(default, _appConfig.AppLaunchPath, RegistryValueKind.ExpandString);

        var inProcServerKey = GetOrCreateSubKey(protonDriveClassIdRegistryKey, "InProcServer32");

        inProcServerKey.SetValue(default, "%SystemRoot%\\System32\\shell32.dll", RegistryValueKind.ExpandString);

        var instanceKey = GetOrCreateSubKey(protonDriveClassIdRegistryKey, "Instance");

        instanceKey.SetValue("CLSID", ShellFileSystemFolderGuid, RegistryValueKind.String);

        var initPropertyBagKey = GetOrCreateSubKey(instanceKey, "InitPropertyBag");

        initPropertyBagKey.SetValue("Attributes", 0x11, RegistryValueKind.DWord);

        initPropertyBagKey.SetValue("TargetFolderPath", path, RegistryValueKind.ExpandString);

        var shellFolderKey = GetOrCreateSubKey(protonDriveClassIdRegistryKey, "ShellFolder");

        shellFolderKey.SetValue("FolderValueFlags", 0x28, RegistryValueKind.DWord);

        shellFolderKey.SetValue("Attributes", unchecked((int)0xF080004D), RegistryValueKind.DWord); // we need to use a signed int.

        var nameSpaceKey = Registry.CurrentUser.OpenSubKey($"{ExplorerKey}\\Desktop\\NameSpace", true)
                           ?? throw new InvalidOperationException($"Registry key '{ExplorerKey}\\Desktop\\NameSpace' not found.");

        var protonDriveNameSpaceKey = GetOrCreateSubKey(nameSpaceKey, ProtonDriveGuid);

        protonDriveNameSpaceKey.SetValue(default, "Proton Drive", RegistryValueKind.String);

        var newStartPanelKey = Registry.CurrentUser.OpenSubKey($"{ExplorerKey}\\HideDesktopIcons\\NewStartPanel", true)
            ?? throw new InvalidOperationException($"Registry {ExplorerKey}\\HideDesktopIcons\\NewStartPanel not found.");

        newStartPanelKey.SetValue(ProtonDriveGuid, 0x1, RegistryValueKind.DWord);
    }

    private void WithLoggedException(Action action)
    {
        _logger.WithLoggedException(action, "Failed to add or remove shell sync folder", includeStackTrace: false);
    }
}
