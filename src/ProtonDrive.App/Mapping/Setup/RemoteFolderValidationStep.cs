using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Drive.Services;
using ProtonDrive.App.Settings;
using ProtonDrive.App.Volumes;
using ProtonDrive.Client;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.App.Mapping.Setup;

internal class RemoteFolderValidationStep : IRemoteFolderValidationStep
{
    private readonly IVolumeService _volumeService;
    private readonly IRemoteFolderService _remoteFolderService;
    private readonly VolumeIdentityProvider _volumeIdentityProvider;
    private readonly ILogger<RemoteFolderValidationStep> _logger;

    public RemoteFolderValidationStep(
        IVolumeService volumeService,
        IRemoteFolderService remoteFolderService,
        VolumeIdentityProvider volumeIdentityProvider,
        ILogger<RemoteFolderValidationStep> logger)
    {
        _volumeService = volumeService;
        _remoteFolderService = remoteFolderService;
        _volumeIdentityProvider = volumeIdentityProvider;
        _logger = logger;
    }

    public async Task<MappingErrorCode?> ValidateAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        try
        {
            return await InternalValidateRemoteFolder(mapping.Remote, mapping.Type, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex.IsDriveClientException())
        {
            _logger.LogError("Remote folder validation failed: {ErrorMessage}", ex.CombinedMessage());
            return MappingErrorCode.DriveAccessFailed;
        }
    }

    private async Task<MappingErrorCode?> InternalValidateRemoteFolder(RemoteReplica replica, MappingType mappingType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(replica.VolumeId) && string.IsNullOrEmpty(replica.ShareId))
        {
            return default;
        }

        var volume = await _volumeService.GetActiveVolumeAsync().ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (volume == null)
        {
            _logger.LogWarning("Active remote volume cannot be obtained");
            return MappingErrorCode.DriveAccessFailed;
        }

        if (!string.IsNullOrEmpty(replica.VolumeId) && replica.VolumeId != volume.Id)
        {
            _logger.LogWarning("Active remote volume has diverged");
            return MappingErrorCode.DriveVolumeDiverged;
        }

        if (mappingType is MappingType.HostDeviceFolder && !string.IsNullOrEmpty(replica.ShareId) && replica.ShareId == volume.RootShareId)
        {
            _logger.LogWarning("Remote share has diverged: foreign device share the same root share ID of the active volume");
            return MappingErrorCode.DriveShareDiverged;
        }

        if (string.IsNullOrEmpty(replica.ShareId) || string.IsNullOrEmpty(replica.RootLinkId))
        {
            return default;
        }

        var folderExist = await _remoteFolderService.FolderExistsAsync(replica.ShareId, replica.RootLinkId, cancellationToken).ConfigureAwait(false);

        if (!folderExist)
        {
            _logger.LogWarning("Remote sync root folder does not exist");
            return MappingErrorCode.DriveShareDiverged;
        }

        AddMissingVolumeInfo(replica);

        return default;
    }

    private void AddMissingVolumeInfo(RemoteReplica replica)
    {
        if (replica.InternalVolumeId != default || string.IsNullOrEmpty(replica.VolumeId))
        {
            return;
        }

        replica.InternalVolumeId = _volumeIdentityProvider.GetRemoteVolumeId(replica.VolumeId);
    }
}
