using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Mapping.Setup.ForeignDevices;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Setup.SharedWithMe.SharedWithMeItem;

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

        var result = mapping.Remote.RootItemType is LinkType.Folder
            ? SetUpLocalFolder(mapping, cancellationToken)
            : SetUpLocalFile(mapping);

        return Task.FromResult(result ?? MappingErrorCode.None);
    }

    private MappingErrorCode? SetUpLocalFolder(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        var localReplica = mapping.Local;
        var localFolderPath = localReplica.Path;

        if (localReplica.IsSetUp())
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!TryCreateLocalFolder(localReplica.Path))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!_localFolderService.TryGetFolderInfo(localFolderPath, FileShare.ReadWrite, out var rootFolder))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        if (rootFolder == null)
        {
            _logger.LogWarning("The local shared with me folder does not exist");
            return MappingErrorCode.LocalFolderDoesNotExist;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!_localFolderService.EmptyFolderExists(localReplica.Path, _specialFolders))
        {
            _logger.LogWarning("The local shared with me folder is not empty");
            return MappingErrorCode.LocalFolderNotEmpty;
        }

        var result = _localFolderIdentityValidator.ValidateFolderIdentity(rootFolder, localReplica, mapping.Remote.RootItemType);
        if (result is not null)
        {
            return result;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!_localFolderService.TryConvertToPlaceholder(localReplica.Path))
        {
            _logger.LogWarning("The local shared with me folder conversion to placeholder failed");
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        localReplica.RootFolderId = rootFolder.Id;
        localReplica.VolumeSerialNumber = rootFolder.VolumeInfo.VolumeSerialNumber;
        localReplica.InternalVolumeId = _volumeIdentityProvider.GetLocalVolumeId(localReplica.VolumeSerialNumber);

        return null;
    }

    private MappingErrorCode? SetUpLocalFile(RemoteToLocalMapping mapping)
    {
        var localReplica = mapping.Local;

        if (localReplica.InternalVolumeId != 0)
        {
            // Already set up
            return null;
        }

        localReplica.InternalVolumeId = _volumeIdentityProvider.GetUniqueLocalVolumeId();

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

            _syncFolderStructureProtector.UnprotectFolder(sharedWithMeRootFolderPath, FolderProtectionType.AncestorWithFiles);

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
