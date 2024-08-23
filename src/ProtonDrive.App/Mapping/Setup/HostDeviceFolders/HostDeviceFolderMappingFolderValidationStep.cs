using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Devices;
using ProtonDrive.App.Settings;
using ProtonDrive.Client;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.Mapping.Setup.HostDeviceFolders;

internal sealed class HostDeviceFolderMappingFolderValidationStep
{
    private readonly IDeviceService _deviceService;
    private readonly Func<FileSystemClientParameters, IFileSystemClient<string>> _remoteFileSystemClientFactory;
    private readonly ILocalFolderValidationStep _localFolderValidation;
    private readonly VolumeIdentityProvider _volumeIdentityProvider;
    private readonly ILogger<HostDeviceFolderMappingFolderValidationStep> _logger;

    public HostDeviceFolderMappingFolderValidationStep(
        IDeviceService deviceService,
        Func<FileSystemClientParameters, IFileSystemClient<string>> remoteFileSystemClientFactory,
        ILocalFolderValidationStep localFolderValidation,
        VolumeIdentityProvider volumeIdentityProvider,
        ILogger<HostDeviceFolderMappingFolderValidationStep> logger)
    {
        _deviceService = deviceService;
        _remoteFileSystemClientFactory = remoteFileSystemClientFactory;
        _localFolderValidation = localFolderValidation;
        _volumeIdentityProvider = volumeIdentityProvider;
        _logger = logger;
    }

    public async Task<MappingErrorCode> ValidateAsync(
        RemoteToLocalMapping mapping,
        IReadOnlySet<string> otherLocalSyncFolders,
        CancellationToken cancellationToken)
    {
        if (mapping.Type is not MappingType.HostDeviceFolder)
        {
            throw new ArgumentException("Mapping type has unexpected value", nameof(mapping));
        }

        var result =
            await ValidateLocalFolderAsync(mapping, otherLocalSyncFolders, cancellationToken).ConfigureAwait(false) ??
            await ValidateRemoteFolderAsync(mapping.Remote, cancellationToken).ConfigureAwait(false);

        return result ?? MappingErrorCode.None;
    }

    private async Task<MappingErrorCode?> ValidateLocalFolderAsync(
        RemoteToLocalMapping mapping,
        IReadOnlySet<string> otherLocalSyncFolders,
        CancellationToken cancellationToken)
    {
        var result = await _localFolderValidation.ValidateAsync(mapping, otherLocalSyncFolders, cancellationToken).ConfigureAwait(false);

        return result is not MappingErrorCode.None ? result : null;
    }

    private Task<MappingErrorCode?> ValidateRemoteFolderAsync(
        RemoteReplica replica,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(replica.VolumeId)
            && string.IsNullOrEmpty(replica.ShareId)
            && string.IsNullOrEmpty(replica.RootLinkId))
        {
            return Task.FromResult(default(MappingErrorCode?));
        }

        Ensure.NotNullOrEmpty(replica.ShareId, nameof(replica), nameof(replica.ShareId));
        Ensure.NotNullOrEmpty(replica.RootLinkId, nameof(replica), nameof(replica.RootLinkId));

        return Safe(UnsafeValidateRemoteFolder);

        async Task<MappingErrorCode?> UnsafeValidateRemoteFolder()
        {
            var hostDevice = await _deviceService.SetUpHostDeviceAsync(cancellationToken).ConfigureAwait(false);
            if (hostDevice is null)
            {
                _logger.LogInformation("Setting up host device failed");
                return MappingErrorCode.DriveAccessFailed;
            }

            if (replica.VolumeId != hostDevice.DataItem.VolumeId)
            {
                _logger.LogWarning("Drive volume has diverged");
                return MappingErrorCode.DriveVolumeDiverged;
            }

            if (replica.ShareId != hostDevice.DataItem.ShareId)
            {
                _logger.LogWarning("Host device has diverged");
                return MappingErrorCode.DriveHostDeviceDiverged;
            }

            var parameters = new FileSystemClientParameters(replica.VolumeId, replica.ShareId);
            var remoteFileSystemClient = _remoteFileSystemClientFactory.Invoke(parameters);
            var folderInfo = NodeInfo<string>.Directory().WithId(replica.RootLinkId);

            try
            {
                folderInfo = await remoteFileSystemClient.GetInfo(folderInfo, cancellationToken).ConfigureAwait(false);
            }
            catch (FileSystemClientException<string> ex) when (ex.ErrorCode is FileSystemErrorCode.ObjectNotFound)
            {
                _logger.LogWarning("Remote host device folder does not exist");
                return MappingErrorCode.DriveFolderDoesNotExist;
            }
            catch (FileSystemClientException ex)
            {
                _logger.LogWarning("Failed to access remote host device folder: {ErrorMessage}", ex.CombinedMessage());
                return MappingErrorCode.DriveAccessFailed;
            }

            if (!string.IsNullOrEmpty(folderInfo.ParentId) && folderInfo.ParentId != hostDevice.DataItem.LinkId)
            {
                _logger.LogWarning("Remote host device folder is not direct child of host device root");
                return MappingErrorCode.DriveFolderDiverged;
            }

            AddMissingVolumeInfo(replica);

            return default;
        }
    }

    private void AddMissingVolumeInfo(RemoteReplica replica)
    {
        if (replica.InternalVolumeId != default || string.IsNullOrEmpty(replica.VolumeId))
        {
            return;
        }

        replica.InternalVolumeId = _volumeIdentityProvider.GetRemoteVolumeId(replica.VolumeId);
    }

    private async Task<MappingErrorCode?> Safe(Func<Task<MappingErrorCode?>> origin)
    {
        try
        {
            return await origin().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex.IsDriveClientException())
        {
            _logger.LogError("Validating remote folder failed: {ErrorMessage}", ex.CombinedMessage());
            return MappingErrorCode.DriveAccessFailed;
        }
    }
}
