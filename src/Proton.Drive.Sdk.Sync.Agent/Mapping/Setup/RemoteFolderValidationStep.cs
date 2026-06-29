using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Drive.Services;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Agent.Volumes;
using Proton.Drive.Sdk.Sync.Client;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Setup;

internal class RemoteFolderValidationStep : IRemoteFolderValidationStep
{
    private readonly IMainVolumeService _mainVolumeService;
    private readonly IRemoteFolderService _remoteFolderService;
    private readonly VolumeIdentityProvider _volumeIdentityProvider;
    private readonly ILogger<RemoteFolderValidationStep> _logger;

    public RemoteFolderValidationStep(
        IMainVolumeService mainVolumeService,
        IRemoteFolderService remoteFolderService,
        VolumeIdentityProvider volumeIdentityProvider,
        ILogger<RemoteFolderValidationStep> logger)
    {
        _mainVolumeService = mainVolumeService;
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
            return null;
        }

        var volume = await _mainVolumeService.GetVolumeAsync().ConfigureAwait(false);

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
            return null;
        }

        var folderExist = await _remoteFolderService.FolderExistsAsync(replica.ShareId, replica.RootLinkId, cancellationToken).ConfigureAwait(false);

        if (!folderExist)
        {
            _logger.LogWarning("Remote sync root folder does not exist");
            return MappingErrorCode.DriveShareDiverged;
        }

        AddMissingVolumeInfo(replica);

        return null;
    }

    private void AddMissingVolumeInfo(RemoteReplica replica)
    {
        if (replica.InternalVolumeId != 0 || string.IsNullOrEmpty(replica.VolumeId))
        {
            return;
        }

        replica.InternalVolumeId = _volumeIdentityProvider.GetRemoteVolumeId(replica.VolumeId);
    }
}
