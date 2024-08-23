using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Settings;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.Mapping.Setup.SharedWithMe.SharedWithMeItemsFolder;

internal sealed class SharedWithMeItemsFolderMappingFoldersSetupStep
{
    private readonly ILocalFolderService _localFolderService;
    private readonly ISyncFolderStructureProtector _syncFolderStructureProtector;
    private readonly LocalFolderIdentityValidator _localFolderIdentityValidator;
    private readonly VolumeIdentityProvider _volumeIdentityProvider;
    private readonly ILogger<SharedWithMeItemsFolderMappingFoldersSetupStep> _logger;

    public SharedWithMeItemsFolderMappingFoldersSetupStep(
        ILocalFolderService localFolderService,
        ISyncFolderStructureProtector syncFolderStructureProtector,
        LocalFolderIdentityValidator localFolderIdentityValidator,
        VolumeIdentityProvider volumeIdentityProvider,
        ILogger<SharedWithMeItemsFolderMappingFoldersSetupStep> logger)
    {
        _localFolderService = localFolderService;
        _syncFolderStructureProtector = syncFolderStructureProtector;
        _localFolderIdentityValidator = localFolderIdentityValidator;
        _volumeIdentityProvider = volumeIdentityProvider;
        _logger = logger;
    }

    public Task<MappingErrorCode> SetUpFoldersAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        if (mapping.Type is not MappingType.SharedWithMeRootFolder)
        {
            throw new ArgumentException("Mapping type has unexpected value", nameof(mapping));
        }

        var result = SetUpLocalFolder(mapping.Local, mapping.Remote.RootLinkType, cancellationToken);

        return Task.FromResult(result ?? MappingErrorCode.None);
    }

    private MappingErrorCode? SetUpLocalFolder(LocalReplica replica, LinkType remoteRootType, CancellationToken cancellationToken)
    {
        if (replica.RootFolderId != default)
        {
            // Already set up
            return default;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!TryCreateLocalFolder(replica.RootFolderPath))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!_localFolderService.TryGetFolderInfo(replica.RootFolderPath, FileShare.ReadWrite, out var rootFolder))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        if (rootFolder == null)
        {
            _logger.LogWarning("The local sync folder does not exist");
            return MappingErrorCode.LocalFolderDoesNotExist;
        }

        cancellationToken.ThrowIfCancellationRequested();

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

            var accountRootFolderPath = Path.GetDirectoryName(path)
                ?? throw new FileSystemClientException("Account root folder path cannot be obtained");

            _syncFolderStructureProtector.Unprotect(accountRootFolderPath, FolderProtectionType.Ancestor);

            try
            {
                Directory.CreateDirectory(path);
            }
            finally
            {
                _syncFolderStructureProtector.Protect(accountRootFolderPath, FolderProtectionType.Ancestor);
            }

            return true;
        }
        catch (Exception ex) when (ex.IsFileAccessException())
        {
            _logger.LogWarning("Failed to create local shared with me items folder: {ExceptionType}: {HResult}", ex.GetType().Name, ex.HResult);

            return false;
        }
    }
}
