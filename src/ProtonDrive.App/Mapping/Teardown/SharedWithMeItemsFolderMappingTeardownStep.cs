using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Settings;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.App.Mapping.Teardown;

internal sealed class SharedWithMeItemsFolderMappingTeardownStep
{
    private readonly ILocalFolderService _localFolderService;
    private readonly ISyncFolderStructureProtector _syncFolderProtector;
    private readonly IOnDemandSyncRootRegistry _onDemandSyncRootRegistry;
    private readonly ILogger<SharedWithMeItemsFolderMappingTeardownStep> _logger;

    public SharedWithMeItemsFolderMappingTeardownStep(
        ILocalFolderService localFolderService,
        ISyncFolderStructureProtector syncFolderProtector,
        IOnDemandSyncRootRegistry onDemandSyncRootRegistry,
        ILogger<SharedWithMeItemsFolderMappingTeardownStep> logger)
    {
        _localFolderService = localFolderService;
        _syncFolderProtector = syncFolderProtector;
        _onDemandSyncRootRegistry = onDemandSyncRootRegistry;
        _logger = logger;
    }

    public async Task<MappingErrorCode> TearDownAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        if (mapping.Type is not MappingType.SharedWithMeRootFolder)
        {
            throw new ArgumentException("Mapping type has unexpected value", nameof(mapping));
        }

        var sharedWithMeItemsFolderPath = mapping.Local.RootFolderPath
            ?? throw new InvalidOperationException("Shared with me items folder path is not specified");

        var accountRootFolderPath = Path.GetDirectoryName(sharedWithMeItemsFolderPath)
            ?? throw new InvalidOperationException("Account root folder path cannot be obtained");

        TryUnprotectLocalFolders();

        if (!await TryRemoveShellFolderAsync(mapping).ConfigureAwait(false)
            || !TryDeleteFolderIfEmpty(sharedWithMeItemsFolderPath))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        _syncFolderProtector.Protect(accountRootFolderPath, FolderProtectionType.Ancestor);

        return MappingErrorCode.None;

        bool TryUnprotectLocalFolders()
        {
            return _syncFolderProtector.Unprotect(accountRootFolderPath, FolderProtectionType.Ancestor) &&
                _syncFolderProtector.Unprotect(sharedWithMeItemsFolderPath, FolderProtectionType.AncestorWithFiles);
        }
    }

    private bool TryDeleteFolderIfEmpty(string folderPath)
    {
        if (_localFolderService.NonEmptyFolderExists(folderPath))
        {
            _logger.LogError("Local shared with me items folder is not empty, skipping deletion");

            return true;
        }

        return TryDeleteFolder(folderPath);
    }

    private bool TryDeleteFolder(string folderPath)
    {
        try
        {
            Directory.Delete(folderPath);
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return true;
        }
        catch (IOException exception) when (exception.HResultContainsWin32ErrorCode(Win32SystemErrorCode.ErrorInvalidName))
        {
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError("Failed to delete local folder: {ExceptionType}: {HResult}", exception.GetType().Name, exception.HResult);

            return false;
        }
    }

    private Task<bool> TryRemoveShellFolderAsync(RemoteToLocalMapping mapping)
    {
        if (mapping.SyncMethod is not SyncMethod.OnDemand)
        {
            throw new InvalidEnumArgumentException(nameof(mapping.SyncMethod), (int)mapping.SyncMethod, typeof(SyncMethod));
        }

        var root = new OnDemandSyncRootInfo(Path: mapping.Local.RootFolderPath, RootId: mapping.Id.ToString(), ShellFolderVisibility.Visible);

        return _onDemandSyncRootRegistry.TryUnregisterAsync(root);
    }
}
