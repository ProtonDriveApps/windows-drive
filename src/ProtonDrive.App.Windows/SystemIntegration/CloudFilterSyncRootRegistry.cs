using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Windows.FileSystem;
using Vanara.PInvoke;
using Windows.Security.Cryptography;
using Windows.Storage;
using Windows.Storage.Provider;

namespace ProtonDrive.App.Windows.SystemIntegration;

internal class CloudFilterSyncRootRegistry : IOnDemandSyncRootRegistry, ISessionStateAware
{
    private const int ErrorCodeElementNotFound = unchecked((int)0x80070490);
    private const string ProviderName = "ProtonDrive";
    private const string SyncRootManagerKeyName = @"Software\Microsoft\Windows\CurrentVersion\Explorer\SyncRootManager";
    private const string DesktopNameSpaceKeyName = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace";
    private const string NamespaceClassIdValueName = "NamespaceCLSID";
    private static readonly Guid ProviderId = Guid.Parse("{87C55815-A77B-4E44-A871-182F19499B54}");

    private readonly AppConfig _appConfig;
    private readonly ILogger<CloudFilterSyncRootRegistry> _logger;

    private string? _userAccountId;

    public CloudFilterSyncRootRegistry(AppConfig appConfig, ILogger<CloudFilterSyncRootRegistry> logger)
    {
        _appConfig = appConfig;
        _logger = logger;
    }

    public static bool TryRemoveAllEntries()
    {
        return TryRemoveAllEntries(NullLogger.Instance);
    }

    public Task<bool> TryRegisterAsync(OnDemandSyncRootInfo root)
    {
        if (!TryGetRootId(root, out var rootId))
        {
            return Task.FromResult(false);
        }

        return TryRegisterSyncRootAsync(rootId, root.Path, root.Visibility);
    }

    public Task<bool> TryUnregisterAsync(OnDemandSyncRootInfo root)
    {
        return Task.FromResult(TryUnregisterSyncRoot(root));
    }

    bool IOnDemandSyncRootRegistry.TryUnregisterAll()
    {
        return TryRemoveAllEntries(_logger);
    }

    void ISessionStateAware.OnSessionStateChanged(SessionState value)
    {
        _userAccountId = value.Status is SessionStatus.Started ? value.UserId : null;
    }

    private static bool TryRemoveAllEntries(ILogger logger)
    {
        try
        {
            var syncRootKey = Registry.LocalMachine.OpenSubKey(SyncRootManagerKeyName)
                              ?? throw new InvalidOperationException($"Registry key '{SyncRootManagerKeyName}' not found");

            var succeeded = true;

            foreach (var subKeyName in syncRootKey.GetSubKeyNames())
            {
                if (subKeyName.StartsWith($"{ProviderName}!"))
                {
                    succeeded &= TryUnregisterSyncRoot(subKeyName, logger);
                }
            }

            return succeeded;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or SecurityException or UnauthorizedAccessException)
        {
            logger.LogWarning("Failed to read registry key {RegistryKeyName}: {ErrorMessage}", SyncRootManagerKeyName, ex.Message);
            return false;
        }
    }

    private static bool TryUnregisterSyncRoot(string rootId, ILogger logger)
    {
        try
        {
            StorageProviderSyncRootManager.Unregister(rootId);

            logger.LogInformation("On-demand sync root \"{RootId}\" unregistered", rootId);
            return true;
        }
        catch (COMException ex) when (ex.ErrorCode == ErrorCodeElementNotFound)
        {
            logger.LogInformation("On-demand sync root \"{RootId}\" does not exist", rootId);
            return true;
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is COMException)
        {
            logger.LogWarning("Failed to unregister on-demand sync root \"{RootId}\": {ErrorCode} {ErrorMessage}", rootId, ex.HResult, ex.Message);
            return false;
        }
    }

    private static void SetInSync(string path)
    {
        using var directory = FileSystemDirectory.Open(path, FileSystemFileAccess.WriteAttributes, FileShare.ReadWrite);

        long usn = 0;

        var result = CldApi.CfSetInSyncState(
            directory.FileHandle,
            CldApi.CF_IN_SYNC_STATE.CF_IN_SYNC_STATE_IN_SYNC,
            CldApi.CF_SET_IN_SYNC_FLAGS.CF_SET_IN_SYNC_FLAG_NONE,
            ref usn);

        Marshal.ThrowExceptionForHR((int)result);
    }

    private async Task<bool> TryRegisterSyncRootAsync(string rootId, string path, ShellFolderVisibility shellFolderVisibility)
    {
        try
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(path);

            var info = new StorageProviderSyncRootInfo
            {
                Id = rootId,
                ProviderId = ProviderId,
                Path = folder,
                DisplayNameResource = _appConfig.AppName,
                ShowSiblingsAsGroup = true,
                Version = _appConfig.AppVersion.ToString(),
                Context = CryptographicBuffer.ConvertStringToBinary(rootId, BinaryStringEncoding.Utf8),
                IconResource = _appConfig.AppLaunchPath,
                AllowPinning = true,
                ProtectionMode = StorageProviderProtectionMode.Unknown,
                HydrationPolicy = StorageProviderHydrationPolicy.Full,
                HydrationPolicyModifier = StorageProviderHydrationPolicyModifier.AutoDehydrationAllowed |
                                          StorageProviderHydrationPolicyModifier.ValidationRequired |
                                          (StorageProviderHydrationPolicyModifier)0x0008, // AllowFullRestartHydration
                PopulationPolicy = StorageProviderPopulationPolicy.AlwaysFull,
                InSyncPolicy = StorageProviderInSyncPolicy.FileSystemAttribute |
                               StorageProviderInSyncPolicy.FileLastWriteTime |
                               StorageProviderInSyncPolicy.DirectorySystemAttribute |
                               StorageProviderInSyncPolicy.DirectoryHiddenAttribute,
                HardlinkPolicy = StorageProviderHardlinkPolicy.None,
            };

            StorageProviderSyncRootManager.Register(info);

            AdjustVisibility(rootId, shellFolderVisibility);

            SetInSync(path);

            _logger.LogInformation("On-demand sync root \"{RootId}\" registered", rootId);
            return true;
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is TypeInitializationException || ex is COMException)
        {
            ex.TryGetRelevantFormattedErrorCode(out var errorCode);
            _logger.LogWarning("Failed to register on-demand sync root \"{RootId}\": {ErrorCode} {ErrorMessage}.", rootId, errorCode, ex.Message);
            return false;
        }
    }

    private void AdjustVisibility(string rootId, ShellFolderVisibility shellFolderVisibility)
    {
        switch (shellFolderVisibility)
        {
            case ShellFolderVisibility.Visible:
                FixShellFolderName(rootId, _appConfig.AppName);
                break;

            case ShellFolderVisibility.Hidden:
                HideSyncRoot(rootId);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(shellFolderVisibility));
        }
    }

    private bool TryUnregisterSyncRoot(OnDemandSyncRootInfo root)
    {
        if (!TryGetRootId(root, out var rootId))
        {
            return false;
        }

        return TryUnregisterSyncRoot(rootId, _logger);
    }

    private void FixShellFolderName(string rootId, string shellFolderName)
    {
        /* StorageProviderSyncRootInfo.DisplayNameResource has no effect when combined with
         * ShowSiblingsAsGroup = true. We update Windows registry for the shell folder to have
         * the expected name.
         */
        try
        {
            var syncRootKey = Registry.LocalMachine.OpenSubKey($"{SyncRootManagerKeyName}\\{rootId}", writable: false)
                              ?? throw new InvalidOperationException($"Registry key '{rootId}' not found");

            var namespaceClassId = syncRootKey.GetValue(NamespaceClassIdValueName) as string
                                   ?? throw new InvalidOperationException($"Registry value '{NamespaceClassIdValueName}' not found");

            var namespaceKey = Registry.ClassesRoot.OpenSubKey($"CLSID\\{namespaceClassId}", writable: true)
                               ?? throw new InvalidOperationException($"Registry key '{namespaceClassId}' not found");

            namespaceKey.SetValue(null, shellFolderName, RegistryValueKind.String);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or SecurityException or UnauthorizedAccessException)
        {
            _logger.LogWarning("Failed to set shell folder name: {ErrorMessage}", ex.Message);
        }
    }

    private void HideSyncRoot(string rootId)
    {
        try
        {
            var syncRootKey = Registry.LocalMachine.OpenSubKey($"{SyncRootManagerKeyName}\\{rootId}", writable: false)
                              ?? throw new InvalidOperationException($"Registry key '{rootId}' not found");

            var namespaceClassId = syncRootKey.GetValue(NamespaceClassIdValueName) as string
                                   ?? throw new InvalidOperationException($"Registry value '{NamespaceClassIdValueName}' not found");

            var desktopNameSpaceKey = Registry.CurrentUser.OpenSubKey(DesktopNameSpaceKeyName, writable: true)
                      ?? throw new InvalidOperationException($"Registry key '{DesktopNameSpaceKeyName}' not found");

            desktopNameSpaceKey.DeleteSubKey(namespaceClassId);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or SecurityException or UnauthorizedAccessException)
        {
            _logger.LogWarning("Failed to set shell folder name: {ErrorMessage}", ex.Message);
        }
    }

    private bool TryGetRootId(OnDemandSyncRootInfo root, [MaybeNullWhen(false)] out string rootId)
    {
        var userSid = WindowsIdentity.GetCurrent().User;

        var userAccountId = _userAccountId;
        if (string.IsNullOrEmpty(userAccountId))
        {
            _logger.LogWarning("Cannot create Cloud Files sync root ID, user account not available");

            rootId = default;
            return false;
        }

        rootId = $"{ProviderName}!{userSid}!{userAccountId}!{root.RootId}";
        return true;
    }
}
