using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Mapping.Setup.ForeignDevices;
using ProtonDrive.App.Settings;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.Mapping.Setup.SharedWithMe.SharedWithMeItem;

internal sealed class SharedWithMeItemMappingSetupStep
{
    private readonly ILocalFolderService _localFolderService;
    private readonly ISyncFolderStructureProtector _syncFolderStructureProtector;
    private readonly LocalFolderIdentityValidator _localFolderIdentityValidator;
    private readonly VolumeIdentityProvider _volumeIdentityProvider;
    private readonly ILogger<ForeignDeviceMappingFoldersSetupStep> _logger;

    private readonly ISet<string> _specialFolders;

    public SharedWithMeItemMappingSetupStep(
        AppConfig appConfig,
        ILocalFolderService localFolderService,
        ISyncFolderStructureProtector syncFolderStructureProtector,
        LocalFolderIdentityValidator localFolderIdentityValidator,
        VolumeIdentityProvider volumeIdentityProvider,
        ILogger<ForeignDeviceMappingFoldersSetupStep> logger)
    {
        _localFolderService = localFolderService;
        _syncFolderStructureProtector = syncFolderStructureProtector;
        _localFolderIdentityValidator = localFolderIdentityValidator;
        _volumeIdentityProvider = volumeIdentityProvider;
        _logger = logger;

        _specialFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            appConfig.FolderNames.BackupFolderName,
            appConfig.FolderNames.TempFolderName,
        };
    }

    public Task<MappingErrorCode> SetUpFoldersAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        if (mapping.Type is not MappingType.SharedWithMeItem)
        {
            throw new ArgumentException("Mapping type has unexpected value", nameof(mapping));
        }

        var result = SetUpLocalFolder(mapping, cancellationToken);

        return Task.FromResult(result ?? MappingErrorCode.None);
    }

    private MappingErrorCode? SetUpLocalFolder(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        var localReplica = mapping.Local;

        if (localReplica.RootFolderId != default)
        {
            // Already set up
            return default;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (mapping.Remote.RootLinkType is LinkType.Folder && !TryCreateLocalFolder(localReplica.RootFolderPath))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!_localFolderService.TryGetFolderInfo(localReplica.RootFolderPath, FileShare.ReadWrite, out var rootFolder))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        if (rootFolder == null)
        {
            _logger.LogWarning("The local sync folder does not exist");
            return MappingErrorCode.LocalFolderDoesNotExist;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (mapping.Remote.RootLinkType is LinkType.Folder && !_localFolderService.EmptyFolderExists(localReplica.RootFolderPath, _specialFolders))
        {
            _logger.LogWarning("The local sync folder is not empty");
            return MappingErrorCode.LocalFolderNotEmpty;
        }

        var result = _localFolderIdentityValidator.ValidateFolderIdentity(rootFolder, localReplica, mapping.Remote.RootLinkType);
        if (result is not null)
        {
            return result;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (mapping.Remote.RootLinkType is LinkType.Folder && !_localFolderService.TryConvertToPlaceholder(localReplica.RootFolderPath))
        {
            _logger.LogWarning("The local sync folder conversion to placeholder failed");
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        localReplica.RootFolderId = rootFolder.Id;
        localReplica.VolumeSerialNumber = rootFolder.VolumeInfo.VolumeSerialNumber;
        localReplica.InternalVolumeId = _volumeIdentityProvider.GetLocalVolumeId(localReplica.VolumeSerialNumber);

        return null;
    }

    private bool TryCreateLocalFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                return true;
            }

            var sharedWithMeRootFolderPath = GetSharedWithMeRootFolderPath(path);

            _syncFolderStructureProtector.Unprotect(sharedWithMeRootFolderPath, FolderProtectionType.AncestorWithFiles);

            Directory.CreateDirectory(path);

            /* Folder protection is added in the finalization step */

            return true;
        }
        catch (Exception ex) when (ex.IsFileAccessException())
        {
            _logger.LogWarning("Failed to create local shared with me folder: {ExceptionType}: {HResult}", ex.GetType().Name, ex.HResult);

            return false;
        }

        static string GetSharedWithMeRootFolderPath(string path)
        {
            var sharedWithMeRootFolderPath = Path.GetDirectoryName(path)
                ?? throw new FileSystemClientException("Shared with me folder path cannot be obtained");

            if (!Directory.Exists(sharedWithMeRootFolderPath))
            {
                throw new FileSystemClientException("Shared with me folder path cannot be obtained");
            }

            return sharedWithMeRootFolderPath;
        }
    }
}
