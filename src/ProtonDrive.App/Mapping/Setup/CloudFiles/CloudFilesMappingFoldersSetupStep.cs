using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Drive.Services;
using ProtonDrive.App.Settings;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.App.Volumes;
using ProtonDrive.Client;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.App.Mapping.Setup.CloudFiles;

internal sealed class CloudFilesMappingFoldersSetupStep
{
    private readonly IVolumeService _volumeService;
    private readonly ILocalFolderService _localFolderService;
    private readonly IRemoteFolderService _remoteFolderService;
    private readonly LocalFolderIdentityValidator _localFolderIdentityValidator;
    private readonly VolumeIdentityProvider _volumeIdentityProvider;
    private readonly ILogger<CloudFilesMappingFoldersSetupStep> _logger;

    private readonly ISet<string> _specialFolders;

    public CloudFilesMappingFoldersSetupStep(
        AppConfig appConfig,
        IVolumeService volumeService,
        ILocalFolderService localFolderService,
        IRemoteFolderService remoteFolderService,
        LocalFolderIdentityValidator localFolderIdentityValidator,
        VolumeIdentityProvider volumeIdentityProvider,
        ILogger<CloudFilesMappingFoldersSetupStep> logger)
    {
        _volumeService = volumeService;
        _localFolderService = localFolderService;
        _remoteFolderService = remoteFolderService;
        _localFolderIdentityValidator = localFolderIdentityValidator;
        _volumeIdentityProvider = volumeIdentityProvider;
        _logger = logger;

        _specialFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            appConfig.FolderNames.BackupFolderName,
            appConfig.FolderNames.TempFolderName,
        };
    }

    public async Task<MappingErrorCode> SetUpFoldersAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        if (mapping.Type is not MappingType.CloudFiles)
        {
            throw new ArgumentException("Mapping type has unexpected value", nameof(mapping));
        }

        var result =
            await SetUpRemoteFolderAsync(mapping.Remote, cancellationToken).ConfigureAwait(false) ??
            SetUpLocalFolder(mapping.Local, mapping.Remote.RootLinkType, cancellationToken) ??
            await ValidateBothFoldersAsync(mapping, cancellationToken).ConfigureAwait(false);

        return result ?? MappingErrorCode.None;
    }

    private Task<MappingErrorCode?> SetUpRemoteFolderAsync(RemoteReplica replica, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Safe(SetUpRemoteFolder);

        async Task<MappingErrorCode?> SetUpRemoteFolder()
        {
            var volume = await _volumeService.GetActiveVolumeAsync().ConfigureAwait(false);

            if (volume == null)
            {
                return MappingErrorCode.DriveAccessFailed;
            }

            replica.VolumeId = volume.Id;
            replica.ShareId = volume.RootShareId;
            replica.RootLinkId = volume.RootLinkId;
            replica.InternalVolumeId = _volumeIdentityProvider.GetRemoteVolumeId(replica.VolumeId);

            return default;
        }
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

        if (!_localFolderService.TryGetFolderInfo(replica.RootFolderPath, FileShare.ReadWrite, out var rootFolder))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        if (rootFolder == null)
        {
            _logger.LogWarning("The local sync folder does not exist");
            return MappingErrorCode.LocalFolderDoesNotExist;
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

    private async Task<MappingErrorCode?> ValidateBothFoldersAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_localFolderService.TryGetFolderInfo(mapping.Local.RootFolderPath, FileShare.ReadWrite, out var rootFolder))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        if (rootFolder == null)
        {
            _logger.LogWarning("The local sync folder does not exist");
            return MappingErrorCode.LocalFolderDoesNotExist;
        }

        if (!mapping.Local.RootFolderId.Equals(rootFolder.Id))
        {
            _logger.LogWarning("The local sync folder identity has diverged");
            return MappingErrorCode.LocalFolderDiverged;
        }

        if (_localFolderService.EmptyFolderExists(mapping.Local.RootFolderPath, _specialFolders))
        {
            return default;
        }

        bool remoteFolderIsEmpty;
        try
        {
            remoteFolderIsEmpty = await _remoteFolderService.NonEmptyFolderExistsAsync(
                    mapping.Remote.ShareId!,
                    mapping.Remote.RootLinkId!,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex.IsDriveClientException())
        {
            _logger.LogError("Failed to access remote folder: {ErrorMessage}", ex.CombinedMessage());
            return MappingErrorCode.DriveAccessFailed;
        }

        if (!remoteFolderIsEmpty)
        {
            _logger.LogWarning("Both the local and remote sync folders are not empty");
            return MappingErrorCode.LocalAndRemoteFoldersNotEmpty;
        }

        return default;
    }

    private bool TryCreateLocalFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);

            return true;
        }
        catch (Exception exception) when (exception.IsFileAccessException())
        {
            _logger.LogError("Failed to create local folder: {ExceptionType}: {HResult}", exception.GetType().Name, exception.HResult);

            return false;
        }
    }

    private async Task<MappingErrorCode?> Safe(Func<Task<MappingErrorCode?>> origin)
    {
        try
        {
            return await origin().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex.IsDriveClientException())
        {
            _logger.LogError("Failed to set up remote folder: {ErrorMessage}", ex.CombinedMessage());
            return MappingErrorCode.DriveAccessFailed;
        }
    }
}
