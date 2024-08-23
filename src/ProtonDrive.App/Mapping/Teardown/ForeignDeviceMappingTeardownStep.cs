using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Settings;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.App.Mapping.Teardown;

internal sealed class ForeignDeviceMappingTeardownStep
{
    private readonly IOnDemandSyncRootRegistry _onDemandSyncRootRegistry;
    private readonly ISyncFolderStructureProtector _syncFolderProtector;
    private readonly ILogger<ForeignDeviceMappingTeardownStep> _logger;

    public ForeignDeviceMappingTeardownStep(
        IOnDemandSyncRootRegistry onDemandSyncRootRegistry,
        ISyncFolderStructureProtector syncFolderProtector,
        ILogger<ForeignDeviceMappingTeardownStep> logger)
    {
        _onDemandSyncRootRegistry = onDemandSyncRootRegistry;
        _syncFolderProtector = syncFolderProtector;
        _logger = logger;
    }

    public async Task<MappingErrorCode> TearDownAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        if (mapping.Type is not MappingType.ForeignDevice)
        {
            throw new ArgumentException("Mapping type has unexpected value", nameof(mapping));
        }

        cancellationToken.ThrowIfCancellationRequested();

        TryUnprotectLocalFolder(mapping);

        if (await TryRemoveOnDemandSyncRootAsync(mapping).ConfigureAwait(false)
            && TryDeleteFolder(mapping.Local.RootFolderPath))
        {
            return MappingErrorCode.None;
        }

        return MappingErrorCode.LocalFileSystemAccessFailed;
    }

    private bool TryUnprotectLocalFolder(RemoteToLocalMapping mapping)
    {
        var foreignDeviceFolderPath = mapping.Local.RootFolderPath;

        // The foreign devices folder ("Other computers") is not unprotected as part of tearing down the foreign device mapping.
        // It is unprotected when tearing down the cloud files mapping.
        return _syncFolderProtector.Unprotect(foreignDeviceFolderPath, FolderProtectionType.Leaf);
    }

    private bool TryDeleteFolder(string folderPath)
    {
        try
        {
            Directory.Delete(folderPath, true);
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

    private async Task<bool> TryRemoveOnDemandSyncRootAsync(RemoteToLocalMapping mapping)
    {
        if (mapping.SyncMethod is not SyncMethod.OnDemand)
        {
            return true;
        }

        var root = new OnDemandSyncRootInfo(Path: mapping.Local.RootFolderPath, RootId: mapping.Id.ToString(), ShellFolderVisibility.Hidden);

        return await _onDemandSyncRootRegistry.TryUnregisterAsync(root).ConfigureAwait(false);
    }
}
