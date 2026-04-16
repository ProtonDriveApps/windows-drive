using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Windows.FileSystem;
using ProtonDrive.Sync.Windows.FileSystem.CloudFiles;
using ProtonDrive.Sync.Windows.Shell;
using Vanara.PInvoke;
using Windows.Security.Cryptography;
using Windows.Storage;
using Windows.Storage.Provider;

namespace ProtonDrive.App.Windows.SystemIntegration;

internal class CloudFilterSyncRootRegistry : IOnDemandSyncRootRegistry, ISessionStateAware
{
    private const StorageProviderHydrationPolicyModifier AllowFullRestartHydration = (StorageProviderHydrationPolicyModifier)0x0008;
    private const int AllowFullRestartHydrationMinPlatformVersionIntegrationNumber = 0x0300;

    private const int ErrorCodeElementNotFound = unchecked((int)0x80070490);
    private const string ProviderName = "ProtonDrive";
    private const string SyncRootManagerKeyName = @"Software\Microsoft\Windows\CurrentVersion\Explorer\SyncRootManager";
    private const string DesktopNameSpaceKeyName = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace";
    private const string NamespaceClassIdValueName = "NamespaceCLSID";
    private const string UserSyncRootsKeyName = "UserSyncRoots";
    private const int SyncRootFlagShowSiblingsAsGroup = 1 << 9;
    private static readonly Guid ProviderId = Guid.Parse("{87C55815-A77B-4E44-A871-182F19499B54}");
    private static readonly IReadOnlyCollection<string> NonSupportedPaths = GetNonSupportedPaths();

    private readonly AppConfig _appConfig;
    private readonly IFileSystemDisplayNameAndIconProvider _fileSystemDisplayNameAndIconProvider;
    private readonly ILogger<CloudFilterSyncRootRegistry> _logger;

    private string? _userAccountId;
    private bool _platformVersionIsLogged;

    public CloudFilterSyncRootRegistry(
        AppConfig appConfig,
        IFileSystemDisplayNameAndIconProvider fileSystemDisplayNameAndIconProvider,
        ILogger<CloudFilterSyncRootRegistry> logger)
    {
        _appConfig = appConfig;
        _fileSystemDisplayNameAndIconProvider = fileSystemDisplayNameAndIconProvider;
        _logger = logger;
    }

    public static bool TryRemoveAllEntries()
    {
        return TryRemoveAllEntries(NullLogger.Instance);
    }

    public async Task<OnDemandSyncRootVerificationResult> VerifyAsync(OnDemandSyncRootInfo root)
    {
        var rootInfo = await CreateSyncRootInfoAsync(root).ConfigureAwait(false);

        if (rootInfo == null)
        {
            return new OnDemandSyncRootVerificationResult(OnDemandSyncRootVerificationVerdict.VerificationFailed);
        }

        var (verdict, conflictingRootInfo) = VerifySyncRoot(rootInfo);

        if (verdict is not OnDemandSyncRootVerificationVerdict.NotRegistered)
        {
            return new OnDemandSyncRootVerificationResult(verdict, GetProviderName(conflictingRootInfo));
        }

        var descendantRootInfo = GetDescendantSyncRoot(rootInfo);

        if (descendantRootInfo is not null)
        {
            return new OnDemandSyncRootVerificationResult(OnDemandSyncRootVerificationVerdict.ConflictingDescendantRootExists, GetProviderName(descendantRootInfo));
        }

        return new OnDemandSyncRootVerificationResult(verdict);
    }

    public async Task<bool> TryRegisterAsync(OnDemandSyncRootInfo root)
    {
        if (!IsPathSupported(root.Path))
        {
            _logger.LogWarning("Failed to register on-demand sync root \"{RootId}\": Non supported path", root.RootId);
            return false;
        }

        var rootInfo = await CreateSyncRootInfoAsync(root).ConfigureAwait(false);

        if (rootInfo == null)
        {
            return false;
        }

        return TryRegisterSyncRoot(rootInfo, root.Visibility);
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

    private static IReadOnlyCollection<string> GetNonSupportedPaths()
    {
        // For some folders, like "%UserProfile%\AppData", on-demand sync root registration succeeds, and it works, but verification fails.
        // Both StorageProviderSyncRootManager.GetSyncRootInformationForFolder and StorageProviderSyncRootManager.GetSyncRootInformationForId
        // throw exception as if the root is not registered. To prevent later failure, we prevent registration on non-supported paths.
        return new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            }
            .Where(x => !string.IsNullOrEmpty(x))
            .Select(PathComparison.EnsureTrailingSeparator)
            .ToList()
            .AsReadOnly();
    }

    private static bool IsPathSupported(string path)
    {
        path = PathComparison.EnsureTrailingSeparator(path);

        return !NonSupportedPaths.Any(nonSupportedPath => path.StartsWith(nonSupportedPath, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryRemoveAllEntries(ILogger logger)
    {
        try
        {
            using var syncRootManagerKey = Registry.LocalMachine.OpenSubKey(SyncRootManagerKeyName, writable: false)
                ?? throw new InvalidOperationException($"Registry key '{SyncRootManagerKeyName}' not found");

            var succeeded = true;

            foreach (var rootId in syncRootManagerKey.GetSubKeyNames().Where(n => n.StartsWith($"{ProviderName}!")))
            {
                TryShowShellFolder(rootId, out _, logger);

                succeeded &= TryUnregisterSyncRoot(rootId, logger);
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

    /// <summary>
    /// Restores Windows registry keys and values to match the status before hiding the shell folder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If we do not restore previous state, the <see cref="StorageProviderSyncRootManager"/> does not
    /// recognize some registry keys as belonging to the sync root. As a result, registering already
    /// registered sync root duplicates registry keys, unregistering leaves registry keys not deleted.
    /// </para>
    /// <para>
    /// A single shell folder serves to multiple sync roots when they are grouped (<see cref="ShellFolderSiblingsGrouping.Grouped"/>).
    /// Upon unregistering a grouped sync root, if other grouped sync roots still exist, the shell folder remains.
    /// </para>
    /// </remarks>
    private static bool TryShowShellFolder(string rootId, out string? hiddenNamespaceClassId, ILogger logger)
    {
        hiddenNamespaceClassId = null;

        try
        {
            using var syncRootKey = Registry.LocalMachine.OpenSubKey($"{SyncRootManagerKeyName}\\{rootId}", writable: false);

            if (syncRootKey is null)
            {
                // Sync root is not registered
                return false;
            }

            var namespaceClassId = syncRootKey.GetValue(NamespaceClassIdValueName) as string
                ?? throw new InvalidOperationException($"Registry value '{NamespaceClassIdValueName}' not found");

            using var desktopNameSpaceKey = Registry.CurrentUser.OpenSubKey(DesktopNameSpaceKeyName, writable: true)
                ?? throw new InvalidOperationException($"Registry key '{DesktopNameSpaceKeyName}' not found");

            // The sub key makes the folder visible in the Windows Explorer
            using var desktopNameSpaceSubKey = desktopNameSpaceKey.CreateSubKey(namespaceClassId)
                ?? throw new InvalidOperationException($"Failed to create or open registry key '{namespaceClassId}'");

            if (desktopNameSpaceSubKey.GetValue(name: null) is not null)
            {
                // Default value already exists, we do not overwrite it
                return true;
            }

            desktopNameSpaceSubKey.SetValue(name: null, rootId, RegistryValueKind.String);

            // Non-null value means the shell folder was hidden, so that the caller knows whether to
            // hide it again upon unregistering the sync root.
            hiddenNamespaceClassId = namespaceClassId;

            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or SecurityException or UnauthorizedAccessException)
        {
            logger.LogWarning("Failed to un-hide shell folder: {ErrorMessage}", ex.Message);
            return false;
        }
    }

    private static void NotifyChanges(string path)
    {
        ShellExtensions.NotifyItemUpdate(path);
    }

    private static string? GetProviderName(StorageProviderSyncRootInfo? rootInfo)
    {
        if (rootInfo is null)
        {
            return null;
        }

        var providerName = rootInfo.DisplayNameResource.Trim();

        if (!string.IsNullOrEmpty(providerName))
        {
            return providerName;
        }

        providerName = rootInfo.Id.Split(['!'], 2)[0];

        if (providerName.Length >= rootInfo.Id.Length)
        {
            return null;
        }

        providerName = providerName.Trim();

        return !string.IsNullOrEmpty(providerName) ? providerName : null;
    }

    private static StorageProviderHydrationPolicyModifier GetAllowFullRestartHydrationFlag()
    {
        var result = CldApi.CfGetPlatformInfo(out var platformVersion);
        Marshal.ThrowExceptionForHR((int)result);

        // According to documentation, flag AllowFullRestartHydration is supported only if the Cloud Files platform IntegrationNumber is 0x500 or higher,
        // thus not before Windows 11. But in fact, it is supported in earlier versions of the platform.
        return platformVersion.IntegrationNumber >= AllowFullRestartHydrationMinPlatformVersionIntegrationNumber
            ? AllowFullRestartHydration
            : default;
    }

    private (OnDemandSyncRootVerificationVerdict Verdict, StorageProviderSyncRootInfo? ConflictingRootInfo) VerifySyncRoot(StorageProviderSyncRootInfo rootInfo)
    {
        LogPlatformVersionOnce();

        try
        {
            var actualInfo = StorageProviderSyncRootManager.GetSyncRootInformationForFolder(rootInfo.Path);

            /* ProviderId and ShowSiblingsAsGroup are not filled by a call to StorageProviderSyncRootManager.GetSyncRootInformationForFolder.
             * If provided folder is not an on-demand root folder, but parent is, parent folder is returned in the Path property.
             *
             * We don't check version to prevent updating registration with each app update. Version is set to app version during registration.
             */

            if (actualInfo.Id == rootInfo.Id &&
                actualInfo.Path.Path.Equals(rootInfo.Path.Path, StringComparison.OrdinalIgnoreCase) &&
                actualInfo.DisplayNameResource == rootInfo.DisplayNameResource &&
                actualInfo.IconResource == rootInfo.IconResource &&
                actualInfo.AllowPinning == rootInfo.AllowPinning &&
                actualInfo.ProtectionMode == rootInfo.ProtectionMode &&
                actualInfo.HydrationPolicy == rootInfo.HydrationPolicy &&
                actualInfo.HydrationPolicyModifier == rootInfo.HydrationPolicyModifier &&
                actualInfo.PopulationPolicy == rootInfo.PopulationPolicy &&
                actualInfo.InSyncPolicy == rootInfo.InSyncPolicy &&
                actualInfo.HardlinkPolicy == rootInfo.HardlinkPolicy)
            {
                if (VerifySyncRootFlag(rootInfo.Path.Path) is var verdict and not OnDemandSyncRootVerificationVerdict.Valid)
                {
                    return (verdict, ConflictingRootInfo: null);
                }

                _logger.LogInformation("On-demand sync root \"{RootId}\" is valid", rootInfo.Id);
                return (OnDemandSyncRootVerificationVerdict.Valid, ConflictingRootInfo: null);
            }

            if (actualInfo.Id != rootInfo.Id)
            {
                _logger.LogWarning("On-demand sync root \"{RootId}\" is conflicting with \"{ConflictingRootId}\"", rootInfo.Id, actualInfo.Id);
                return (OnDemandSyncRootVerificationVerdict.ConflictingRootExists, ConflictingRootInfo: actualInfo);
            }

            LogInvalidSyncRoot(rootInfo, actualInfo);

            if (VerifySyncRootFlag(rootInfo.Path.Path) is var verdict2 and not OnDemandSyncRootVerificationVerdict.Valid)
            {
                return (verdict2, ConflictingRootInfo: null);
            }

            if (ValidateActualSyncRootInfo(actualInfo) is var verdict3 and not OnDemandSyncRootVerificationVerdict.Valid)
            {
                return (verdict3, ConflictingRootInfo: null);
            }

            return (OnDemandSyncRootVerificationVerdict.Invalid, ConflictingRootInfo: null);
        }
        catch (COMException ex) when (ex.ErrorCode == ErrorCodeElementNotFound)
        {
            _logger.LogWarning("On-demand sync root \"{RootId}\" does not exist", rootInfo.Id);
            return (OnDemandSyncRootVerificationVerdict.NotRegistered, ConflictingRootInfo: null);
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is TypeInitializationException || ex is COMException)
        {
            ex.TryGetRelevantFormattedErrorCode(out var errorCode);
            _logger.LogError("Failed to verify on-demand sync root \"{RootId}\": {ErrorCode} {ErrorMessage}", rootInfo.Id, errorCode, ex.Message);
            return (OnDemandSyncRootVerificationVerdict.VerificationFailed, ConflictingRootInfo: null);
        }
    }

    private StorageProviderSyncRootInfo? GetDescendantSyncRoot(StorageProviderSyncRootInfo rootInfo)
    {
        try
        {
            var existingSyncRoots = StorageProviderSyncRootManager.GetCurrentSyncRoots();
            var descendantRootInfo = existingSyncRoots.FirstOrDefault(x => PathComparison.IsAncestor(rootInfo.Path.Path, x.Path.Path));

            if (descendantRootInfo is null)
            {
                return null;
            }

            _logger.LogWarning("On-demand sync root \"{RootId}\" is conflicting with descendant \"{ConflictingRootId}\"", rootInfo.Id, descendantRootInfo.Id);
            return descendantRootInfo;
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is TypeInitializationException || ex is COMException)
        {
            ex.TryGetRelevantFormattedErrorCode(out var errorCode);
            _logger.LogError("Failed to get descendants of on-demand sync root \"{RootId}\": {ErrorCode}: {ErrorMessage}.", rootInfo.Id, errorCode, ex.Message);
            return null;
        }
    }

    private bool TryRegisterSyncRoot(StorageProviderSyncRootInfo rootInfo, ShellFolderVisibility shellFolderVisibility)
    {
        LogPlatformVersionOnce();

        try
        {
            RestoreVisibility(rootInfo);

            StorageProviderSyncRootManager.Register(rootInfo);

            AdjustVisibility(rootInfo, shellFolderVisibility);

            SetInSync(rootInfo.Path.Path);

            NotifyChanges(rootInfo.Path.Path);

            _logger.LogInformation("On-demand sync root \"{RootId}\" registered", rootInfo.Id);
            return true;
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is TypeInitializationException || ex is COMException)
        {
            ex.TryGetRelevantFormattedErrorCode(out var errorCode);

            if (errorCode is "0x80040111")
            {
                _logger.LogError("Failed to register on-demand sync root: Cloud Files not supported");
            }

            _logger.LogError("Failed to register on-demand sync root \"{RootId}\": {ErrorCode}: {ErrorMessage}", rootInfo.Id, errorCode, ex.Message);
            return false;
        }
    }

    private async Task<StorageProviderSyncRootInfo?> CreateSyncRootInfoAsync(OnDemandSyncRootInfo root)
    {
        if (!TryGetRootId(root, out var rootId))
        {
            return null;
        }

        try
        {
            // StorageFolder.GetFolderFromPathAsync throws UnauthorizedAccessException if the folder is marked with system or hidden file attributes
            var folder = await StorageFolder.GetFolderFromPathAsync(root.Path);
            var displayName = _appConfig.AppName + " - " + _fileSystemDisplayNameAndIconProvider.GetDisplayNameWithoutAccess(root.Path);

            return new StorageProviderSyncRootInfo
            {
                Id = rootId,
                ProviderId = ProviderId,
                Path = folder,
                DisplayNameResource = displayName,
                ShowSiblingsAsGroup = root.SiblingsGrouping is ShellFolderSiblingsGrouping.Grouped,
                Version = _appConfig.AppVersion.ToString(),
                Context = CryptographicBuffer.ConvertStringToBinary(rootId, BinaryStringEncoding.Utf8),
                IconResource = _appConfig.AppLaunchPath,
                AllowPinning = true,
                ProtectionMode = StorageProviderProtectionMode.Unknown,
                HydrationPolicy = StorageProviderHydrationPolicy.Full,
                HydrationPolicyModifier =
                    StorageProviderHydrationPolicyModifier.AutoDehydrationAllowed |
                    StorageProviderHydrationPolicyModifier.ValidationRequired |
                    GetAllowFullRestartHydrationFlag(),
                PopulationPolicy = StorageProviderPopulationPolicy.AlwaysFull,
                InSyncPolicy =
                    StorageProviderInSyncPolicy.FileSystemAttribute |
                    StorageProviderInSyncPolicy.FileLastWriteTime |
                    StorageProviderInSyncPolicy.DirectorySystemAttribute |
                    StorageProviderInSyncPolicy.DirectoryHiddenAttribute,
                HardlinkPolicy = StorageProviderHardlinkPolicy.None,
            };
        }
        catch (ArgumentException ex)
        {
            // StorageFolder.GetFolderFromPathAsync sometimes throws ArgumentException claiming that
            // an item cannot be found at the specified path.
            _logger.LogError("Failed to create sync root info \"{RootId}\": {ErrorMessage}", rootId, ex.Message);
            return null;
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is TypeInitializationException || ex is COMException)
        {
            ex.TryGetRelevantFormattedErrorCode(out var errorCode);
            _logger.LogError("Failed to create sync root info \"{RootId}\": {ErrorCode} {ErrorMessage}", rootId, errorCode, ex.Message);
            return null;
        }
    }

    private void AdjustVisibility(StorageProviderSyncRootInfo rootInfo, ShellFolderVisibility shellFolderVisibility)
    {
        if (rootInfo.ShowSiblingsAsGroup)
        {
            FixShellFolderName(rootInfo.Id, _appConfig.AppName);
        }

        if (shellFolderVisibility is ShellFolderVisibility.Hidden)
        {
            HideShellFolder(rootInfo.Id);
        }
    }

    private void RestoreVisibility(StorageProviderSyncRootInfo rootInfo)
    {
        if (TryShowShellFolder(rootInfo.Id, out _, _logger))
        {
            return;
        }

        if (!rootInfo.ShowSiblingsAsGroup)
        {
            return;
        }

        var siblingRootId = GetSiblingRootIdByPath(rootInfo.Path.Path);
        if (string.IsNullOrEmpty(siblingRootId))
        {
            return;
        }

        TryShowShellFolder(siblingRootId, out _, _logger);
    }

    private bool TryUnregisterSyncRoot(OnDemandSyncRootInfo root)
    {
        if (!TryGetRootId(root, out var rootId))
        {
            return false;
        }

        TryShowShellFolder(rootId, out var namespaceClassId, _logger);

        if (TryUnregisterSyncRoot(rootId, _logger))
        {
            HideShellFolderByNamespaceClass(namespaceClassId);

            NotifyChanges(root.Path);

            return true;
        }

        HideShellFolder(rootId);

        return false;
    }

    /// <summary>
    /// <see cref="StorageProviderSyncRootInfo.DisplayNameResource"/> is used by Windows Storage Sense and in the shell.
    /// When combined with ShowSiblingsAsGroup = true, the shell displays the parent folder name instead.
    /// We update Windows registry for the shell folder to have the expected name.
    /// </summary>
    private void FixShellFolderName(string rootId, string shellFolderName)
    {
        try
        {
            using var syncRootKey = Registry.LocalMachine.OpenSubKey($"{SyncRootManagerKeyName}\\{rootId}", writable: false)
                              ?? throw new InvalidOperationException($"Registry key '{rootId}' not found");

            var namespaceClassId = syncRootKey.GetValue(NamespaceClassIdValueName) as string
                                   ?? throw new InvalidOperationException($"Registry value '{NamespaceClassIdValueName}' not found");

            using var namespaceKey = Registry.ClassesRoot.OpenSubKey($"CLSID\\{namespaceClassId}", writable: true)
                               ?? throw new InvalidOperationException($"Registry key '{namespaceClassId}' not found");

            namespaceKey.SetValue(null, shellFolderName, RegistryValueKind.String);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or SecurityException or UnauthorizedAccessException)
        {
            _logger.LogWarning("Failed to set shell folder name: {ErrorMessage}", ex.Message);
        }
    }

    private void HideShellFolder(string rootId)
    {
        try
        {
            using var syncRootKey = Registry.LocalMachine.OpenSubKey($"{SyncRootManagerKeyName}\\{rootId}", writable: false)
                              ?? throw new InvalidOperationException($"Registry key '{rootId}' not found");

            var namespaceClassId = syncRootKey.GetValue(NamespaceClassIdValueName) as string
                                   ?? throw new InvalidOperationException($"Registry value '{NamespaceClassIdValueName}' not found");

            HideShellFolderByNamespaceClass(namespaceClassId);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or SecurityException or UnauthorizedAccessException)
        {
            _logger.LogWarning("Failed to hide shell folder: {ErrorMessage}", ex.Message);
        }
    }

    private void HideShellFolderByNamespaceClass(string? namespaceClassId)
    {
        if (string.IsNullOrEmpty(namespaceClassId))
        {
            return;
        }

        try
        {
            using var desktopNameSpaceKey = Registry.CurrentUser.OpenSubKey(DesktopNameSpaceKeyName, writable: true)
                      ?? throw new InvalidOperationException($"Registry key '{DesktopNameSpaceKeyName}' not found");

            desktopNameSpaceKey.DeleteSubKey(namespaceClassId, throwOnMissingSubKey: false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or SecurityException or UnauthorizedAccessException)
        {
            _logger.LogWarning("Failed to hide shell folder: {ErrorMessage}", ex.Message);
        }
    }

    private string? GetSiblingRootIdByPath(string path)
    {
        var groupFolderPath = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(groupFolderPath))
        {
            return null;
        }

        try
        {
            using var syncRootManagerKey = Registry.LocalMachine.OpenSubKey(SyncRootManagerKeyName, writable: false)
                ?? throw new InvalidOperationException($"Registry key '{SyncRootManagerKeyName}' not found");

            foreach (var rootId in syncRootManagerKey.GetSubKeyNames().Where(n => n.StartsWith($"{ProviderName}!")))
            {
                using var syncRootKey = syncRootManagerKey.OpenSubKey(rootId, writable: false);
                if (syncRootKey == null)
                {
                    continue;
                }

                var flags = (int?)syncRootKey.GetValue("Flags") ?? 0;
                if ((flags & SyncRootFlagShowSiblingsAsGroup) == 0)
                {
                    continue;
                }

                using var userSyncRootsKey = syncRootKey.OpenSubKey(UserSyncRootsKeyName);

                var valueName = userSyncRootsKey?.GetValueNames().FirstOrDefault();
                if (valueName == null)
                {
                    continue;
                }

                var folderPath = (string?)userSyncRootsKey?.GetValue(valueName, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (string.IsNullOrEmpty(folderPath))
                {
                    continue;
                }

                var parentFolderPath = Path.GetDirectoryName(folderPath);
                if (string.IsNullOrEmpty(parentFolderPath))
                {
                    continue;
                }

                if (!parentFolderPath.Equals(groupFolderPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return rootId;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or SecurityException or UnauthorizedAccessException)
        {
            _logger.LogWarning("Failed to get sibling root ID: {ErrorMessage}", ex.Message);
        }

        return null;
    }

    private bool TryGetRootId(OnDemandSyncRootInfo root, [MaybeNullWhen(false)] out string rootId)
    {
        var userSid = WindowsIdentity.GetCurrent().User;

        var userAccountId = _userAccountId;
        if (string.IsNullOrEmpty(userAccountId))
        {
            _logger.LogWarning("Cannot create Cloud Files sync root ID, user account not available");

            rootId = null;
            return false;
        }

        rootId = $"{ProviderName}!{userSid}!{userAccountId}!{root.RootId}";
        return true;
    }

    private OnDemandSyncRootVerificationVerdict VerifySyncRootFlag(string path)
    {
        var placeholderState = GetPlaceholderState(path);

        if (placeholderState is PlaceholderState.Invalid)
        {
            _logger.LogError("Root folder placeholder state is {PlaceholderState}", placeholderState);
            return OnDemandSyncRootVerificationVerdict.VerificationFailed;
        }

        if (!placeholderState.HasFlag(PlaceholderState.SyncRoot))
        {
            _logger.LogError("Root folder placeholder state is {PlaceholderState}", placeholderState);
            return OnDemandSyncRootVerificationVerdict.MissingSyncRootFlag;
        }

        return OnDemandSyncRootVerificationVerdict.Valid;
    }

    private OnDemandSyncRootVerificationVerdict ValidateActualSyncRootInfo(StorageProviderSyncRootInfo actualInfo)
    {
        if (actualInfo.HydrationPolicyModifier is StorageProviderHydrationPolicyModifier.None &&
            actualInfo.InSyncPolicy is StorageProviderInSyncPolicy.Default)
        {
            _logger.LogError("On-demand sync root state is invalid");
            return OnDemandSyncRootVerificationVerdict.VerificationFailed;
        }

        return OnDemandSyncRootVerificationVerdict.Valid;
    }

    private PlaceholderState GetPlaceholderState(string path)
    {
        try
        {
            using var directory = FileSystemDirectory.Open(path, FileSystemFileAccess.ReadAttributes, FileShare.ReadWrite);

            return directory.GetPlaceholderState();
        }
        catch (Exception ex) when (ex.IsFileAccessException())
        {
            _logger.LogWarning("Failed to get placeholder state: {ErrorCode}: {ErrorMessage}", ex.GetRelevantFormattedErrorCode(), ex.Message);

            return PlaceholderState.Invalid;
        }
    }

    private void SetInSync(string path)
    {
        try
        {
            using var directory = FileSystemDirectory.Open(path, FileSystemFileAccess.WriteAttributes, FileShare.ReadWrite);

            var result = CldApi.CfSetInSyncState(
                directory.FileHandle,
                CldApi.CF_IN_SYNC_STATE.CF_IN_SYNC_STATE_IN_SYNC,
                CldApi.CF_SET_IN_SYNC_FLAGS.CF_SET_IN_SYNC_FLAG_NONE);

            Marshal.ThrowExceptionForHR((int)result);
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is COMException)
        {
            _logger.LogWarning("Failed to set folder in-sync: {ErrorCode}: {ErrorMessage}", ex.GetRelevantFormattedErrorCode(), ex.Message);
        }
    }

    private void LogPlatformVersionOnce()
    {
        if (_platformVersionIsLogged)
        {
            return;
        }

        try
        {
            var result = CldApi.CfGetPlatformInfo(out var platformVersion);

            Marshal.ThrowExceptionForHR((int)result);

            _logger.LogInformation(
                "Cloud Files platform version: RevisionNumber = {RevisionNumber:x8}, BuildNumber = {BuildNumber:x8}, IntegrationNumber = {IntegrationNumber:x8}",
                platformVersion.RevisionNumber,
                platformVersion.BuildNumber,
                platformVersion.IntegrationNumber);

            if (platformVersion.IntegrationNumber < AllowFullRestartHydrationMinPlatformVersionIntegrationNumber)
            {
                _logger.LogWarning("Cloud Files platform does not support restarting hydration");
            }

            _platformVersionIsLogged = true;
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is COMException)
        {
            _logger.LogError("Failed to obtain Cloud Files platform version: {ErrorCode}: {ErrorMessage}", ex.GetRelevantFormattedErrorCode(), ex.Message);
            _platformVersionIsLogged = true;
        }
    }

    private void LogInvalidSyncRoot(StorageProviderSyncRootInfo expected, StorageProviderSyncRootInfo actual)
    {
        _logger.LogWarning("On-demand sync root \"{RootId}\" is not valid, version is \"{ActualVersion}\"", expected.Id, actual.Version);

        if (!actual.Path.Path.Equals(expected.Path.Path, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "On-demand sync root path \"{ActualPath}\" is not expected, should be \"{ExpectedPath}\"",
                actual.Path.Path,
                expected.Path.Path);
        }

        if (actual.DisplayNameResource != expected.DisplayNameResource)
        {
            _logger.LogWarning(
                "On-demand sync root display name \"{ActualDisplayName}\" is not expected, should be \"{ExpectedDisplayName}\"",
                actual.DisplayNameResource,
                expected.DisplayNameResource);
        }

        if (actual.IconResource != expected.IconResource)
        {
            _logger.LogWarning("On-demand sync root icon resource is not expected");
        }

        if (actual.AllowPinning != expected.AllowPinning)
        {
            _logger.LogWarning(
                "On-demand sync root allow pining flag \"{ActualAllowPinning}\" is not expected, should be \"{ExpectedAllowPinning}\"",
                actual.AllowPinning,
                expected.AllowPinning);
        }

        if (actual.ProtectionMode != expected.ProtectionMode)
        {
            _logger.LogWarning(
                "On-demand sync root protection mode {ActualProtectionMode} is not expected, should be {ExpectedProtectionMode}",
                actual.ProtectionMode,
                expected.ProtectionMode);
        }

        if (actual.HydrationPolicy != expected.HydrationPolicy)
        {
            _logger.LogWarning(
                "On-demand sync root hydration policy {ActualHydrationPolicy} is not expected, should be {ExpectedHydrationPolicy}",
                actual.HydrationPolicy,
                expected.HydrationPolicy);
        }

        if (actual.HydrationPolicyModifier != expected.HydrationPolicyModifier)
        {
            _logger.LogWarning(
                "On-demand sync root hydration policy modifier {ActualHydrationPolicyModifier} is not expected, should be {ExpectedHydrationPolicyModifier}",
                actual.HydrationPolicyModifier,
                expected.HydrationPolicyModifier);
        }

        if (actual.PopulationPolicy != expected.PopulationPolicy)
        {
            _logger.LogWarning(
                "On-demand sync root population policy {ActualPopulationPolicy} is not expected, should be {ExpectedPopulationPolicy}",
                actual.PopulationPolicy,
                expected.PopulationPolicy);
        }

        if (actual.InSyncPolicy != expected.InSyncPolicy)
        {
            _logger.LogWarning(
                "On-demand sync root in-sync policy {ActualInSyncPolicy} is not expected, should be {ExpectedInSyncPolicy}",
                actual.InSyncPolicy,
                expected.InSyncPolicy);
        }

        if (actual.HardlinkPolicy != expected.HardlinkPolicy)
        {
            _logger.LogWarning(
                "On-demand sync root hard link policy {ActualHardlinkPolicy} is not expected, should be {ExpectedHardlinkPolicy}",
                actual.HardlinkPolicy,
                expected.HardlinkPolicy);
        }
    }
}
