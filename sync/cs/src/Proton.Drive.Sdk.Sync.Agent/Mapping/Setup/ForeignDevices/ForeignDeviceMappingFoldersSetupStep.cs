using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Setup.ForeignDevices;

internal sealed class ForeignDeviceMappingFoldersSetupStep
{
    private readonly ILocalFolderService _localFolderService;
    private readonly ISyncFolderStructureProtector _syncFolderStructureProtector;
    private readonly LocalFolderIdentityValidator _localFolderIdentityValidator;
    private readonly VolumeIdentityProvider _volumeIdentityProvider;
    private readonly ILogger<ForeignDeviceMappingFoldersSetupStep> _logger;

    private readonly ISet<string> _specialFolders;

    public ForeignDeviceMappingFoldersSetupStep(
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
        if (mapping.Type is not MappingType.ForeignDevice)
        {
            throw new ArgumentException("Mapping type has unexpected value", nameof(mapping));
        }

        var result = SetUpLocalFolder(mapping.Local, mapping.Remote.RootItemType, cancellationToken);

        return Task.FromResult(result ?? MappingErrorCode.None);
    }

    private MappingErrorCode? SetUpLocalFolder(LocalReplica replica, LinkType remoteRootType, CancellationToken cancellationToken)
    {
        if (replica.IsSetUp())
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!TryCreateLocalFolder(replica.Path))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!_localFolderService.TryGetFolderInfo(replica.Path, FileShare.ReadWrite, out var rootFolder))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        if (rootFolder == null)
        {
            _logger.LogWarning("The local sync folder does not exist");
            return MappingErrorCode.LocalFolderDoesNotExist;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!_localFolderService.EmptyFolderExists(replica.Path, _specialFolders))
        {
            _logger.LogWarning("The local sync folder is not empty");
            return MappingErrorCode.LocalFolderNotEmpty;
        }

        var result = _localFolderIdentityValidator.ValidateFolderIdentity(rootFolder, replica, remoteRootType);
        if (result is not null)
        {
            return result;
        }

        replica.RootFolderId = rootFolder.Id;
        replica.VolumeSerialNumber = rootFolder.VolumeInfo.VolumeSerialNumber;
        replica.InternalVolumeId = _volumeIdentityProvider.GetLocalVolumeId(replica.VolumeSerialNumber);

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

            var foreignDevicesFolderPath = GetForeignDevicesFolderPath(path);

            _syncFolderStructureProtector.UnprotectFolder(foreignDevicesFolderPath, FolderProtectionType.Ancestor);

            Directory.CreateDirectory(path);

            /* Foreign devices folder protection is added in the finalization step */

            return true;
        }
        catch (Exception ex) when (ex.IsFileAccessException())
        {
            _logger.LogWarning("Failed to create local foreign device folder: {ExceptionType}: {HResult}", ex.GetType().Name, ex.HResult);

            return false;
        }
    }

    private string GetForeignDevicesFolderPath(string path)
    {
        var foreignDevicesFolderPath = Path.GetDirectoryName(path)
                                       ?? throw new FileSystemClientException("Foreign devices folder path cannot be obtained");

        if (Directory.Exists(foreignDevicesFolderPath))
        {
            return foreignDevicesFolderPath;
        }

        var accountRootFolderPath = Path.GetDirectoryName(foreignDevicesFolderPath)
                                    ?? throw new FileSystemClientException("Account root folder path cannot be obtained");

        _syncFolderStructureProtector.UnprotectFolder(accountRootFolderPath, FolderProtectionType.Ancestor);

        try
        {
            Directory.CreateDirectory(foreignDevicesFolderPath);
        }
        finally
        {
            _syncFolderStructureProtector.ProtectFolder(accountRootFolderPath, FolderProtectionType.Ancestor);
        }

        return foreignDevicesFolderPath;
    }
}
