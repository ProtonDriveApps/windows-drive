using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Settings;
using ProtonDrive.Client;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Client.Shares.SharedWithMe;
using ProtonDrive.Shared.Extensions;
using SharedWithMeItem = ProtonDrive.Client.Shares.SharedWithMe.SharedWithMeItem;

namespace ProtonDrive.App.Mapping.Setup;

internal class RemoteSharedWithMeItemValidationStep : IRemoteSharedWithMeItemValidationStep
{
    private readonly ISharedWithMeClient _sharedWithMeClient;
    private readonly VolumeIdentityProvider _volumeIdentityProvider;
    private readonly ILogger<RemoteSharedWithMeItemValidationStep> _logger;

    public RemoteSharedWithMeItemValidationStep(
        ISharedWithMeClient sharedWithMeClient,
        VolumeIdentityProvider volumeIdentityProvider,
        ILogger<RemoteSharedWithMeItemValidationStep> logger)
    {
        _sharedWithMeClient = sharedWithMeClient;
        _volumeIdentityProvider = volumeIdentityProvider;
        _logger = logger;
    }

    public async Task<MappingErrorCode?> ValidateAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        try
        {
            return await ValidateRemoteFolderAsync(mapping.Remote, mapping.Type, cancellationToken).ConfigureAwait(false);
        }
        catch (ApiException ex) when (ex.ResponseCode == ResponseCode.DoesNotExist)
        {
            _logger.LogError("Remote shared with me item does not exist: {ErrorMessage}", ex.CombinedMessage());
            return MappingErrorCode.DriveFolderDoesNotExist;
        }
        catch (Exception ex) when (ex.IsDriveClientException())
        {
            _logger.LogError("Remote shared with me item validation failed: {ErrorMessage}", ex.CombinedMessage());
            return MappingErrorCode.DriveAccessFailed;
        }
    }

    private static bool IsFolder(MappingType mappingType)
    {
        return mappingType switch
        {
            MappingType.SharedWithMeItem => true,
            _ => throw new InvalidEnumArgumentException(nameof(mappingType), (int)mappingType, typeof(MappingType)),
        };
    }

    private static string GetItemTypeName(SharedWithMeItem item)
    {
        return item.IsFolder ? "Folder" : "File";
    }

    private async Task<MappingErrorCode?> ValidateRemoteFolderAsync(RemoteReplica replica, MappingType mappingType, CancellationToken cancellationToken)
    {
        if (replica.ShareId is null)
        {
            ArgumentNullException.ThrowIfNull(replica.ShareId);
        }

        var sharedWithMeItem = await _sharedWithMeClient.GetSharedWithMeItemAsync(replica.ShareId, cancellationToken).ConfigureAwait(false);

        if (sharedWithMeItem is null)
        {
            _logger.LogError("Remote shared with me item does not exist");
            return MappingErrorCode.DriveFolderDoesNotExist;
        }

        if (replica.RootLinkType is LinkType.Folder && sharedWithMeItem.IsFolder != IsFolder(mappingType))
        {
            _logger.LogError("Remote shared with me item type is not expected: {ItemType}", GetItemTypeName(sharedWithMeItem));
            return MappingErrorCode.DriveFolderDoesNotExist;
        }

        if (sharedWithMeItem.VolumeId != replica.VolumeId
            || sharedWithMeItem.LinkId != replica.RootLinkId)
        {
            _logger.LogError("Remote shared with me item has diverged: Volume or Link is not expected");
            return MappingErrorCode.DriveFolderDiverged;
        }

        if (sharedWithMeItem.IsReadOnly)
        {
            _logger.LogError("Remote shared with me item is read-only");
            return MappingErrorCode.DriveFolderDiverged;
        }

        // WHY??
        //replica.RootFolderName = sharedWithMeItem.Name;

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
