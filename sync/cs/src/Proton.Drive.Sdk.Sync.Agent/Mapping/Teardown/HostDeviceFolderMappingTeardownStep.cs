using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Mapping.Setup.HostDeviceFolders;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.OnDemand;
using Proton.Drive.Sdk.Sync.Shared.Shell;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Teardown;

internal sealed class HostDeviceFolderMappingTeardownStep
{
    private readonly ILocalSpecialSubfoldersDeletionStep _specialFoldersDeletion;
    private readonly IOnDemandSyncRootRegistry _onDemandSyncRootRegistry;
    private readonly IPlaceholderToRegularItemConverter _placeholderConverter;
    private readonly IRemoteFileSystemClientFactory _remoteFileSystemClientFactory;
    private readonly ILogger<HostDeviceFolderMappingFoldersSetupStep> _logger;

    public HostDeviceFolderMappingTeardownStep(
        ILocalSpecialSubfoldersDeletionStep specialFoldersDeletion,
        IOnDemandSyncRootRegistry onDemandSyncRootRegistry,
        IPlaceholderToRegularItemConverter placeholderConverter,
        IRemoteFileSystemClientFactory remoteFileSystemClientFactory,
        ILogger<HostDeviceFolderMappingFoldersSetupStep> logger)
    {
        _specialFoldersDeletion = specialFoldersDeletion;
        _onDemandSyncRootRegistry = onDemandSyncRootRegistry;
        _placeholderConverter = placeholderConverter;
        _remoteFileSystemClientFactory = remoteFileSystemClientFactory;
        _logger = logger;
    }

    public async Task<MappingErrorCode> TearDownAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        if (mapping.Type is not MappingType.HostDeviceFolder)
        {
            throw new ArgumentException("Mapping type has unexpected value", nameof(mapping));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!TryConvertToRegularFolder(mapping))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        if (!TryDeleteSpecialSubfolders(mapping))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        if (!await TryRemoveOnDemandSyncRootAsync(mapping).ConfigureAwait(false))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        await DeleteRemoteHostDeviceFolder(mapping.Remote, cancellationToken).ConfigureAwait(false);

        return MappingErrorCode.None;
    }

    private async Task DeleteRemoteHostDeviceFolder(RemoteReplica replica, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(replica.RootLinkId) || string.IsNullOrEmpty(replica.ShareId))
        {
            return;
        }

        await DeleteDeviceFolderAsync(replica.VolumeId ?? string.Empty, replica.ShareId, replica.RootLinkId, cancellationToken).ConfigureAwait(false);
    }

    private async Task DeleteDeviceFolderAsync(string volumeId, string shareId, string id, CancellationToken cancellationToken)
    {
        var parameters = new FileSystemClientParameters(volumeId, shareId);
        var fileSystemClient = _remoteFileSystemClientFactory.CreateClient(parameters);

        var folderInfo = NodeInfo<string>.Directory().WithId(id);

        try
        {
            await fileSystemClient.DeleteAsync(folderInfo, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Moved to trash remote host device folder with ID={Id}", id);
        }
        catch (FileSystemClientException<string> ex) when (ex.ErrorCode == FileSystemErrorCode.ObjectNotFound)
        {
            // Success
        }
        catch (FileSystemClientException<string> ex)
        {
            // Errors are silently ignored for now
            _logger.LogWarning(
                "Moving to trash remote host device folder failed: {ErrorMessage}",
                ex.CombinedMessage());
        }
    }

    private bool TryConvertToRegularFolder(RemoteToLocalMapping mapping)
    {
        // The folder might belong to on-demand sync root registered by a third-party application.
        // To avoid interference with third-party applications, attempt conversion to regular
        // folder only if the application has successfully synced it on-demand.
        if (mapping.SyncMethod is not SyncMethod.OnDemand)
        {
            return true;
        }

        return _placeholderConverter.TryConvertToRegularFolder(mapping.Local.Path, skipRoot: true);
    }

    private bool TryDeleteSpecialSubfolders(RemoteToLocalMapping mapping)
    {
        _specialFoldersDeletion.DeleteSpecialSubfolders(mapping.Local.Path);

        return true;
    }

    private async Task<bool> TryRemoveOnDemandSyncRootAsync(RemoteToLocalMapping mapping)
    {
        if (!mapping.IsOrCouldBeConvertedToOnDemand())
        {
            return true;
        }

        var root = new OnDemandSyncRootInfo(
            Path: mapping.Local.Path,
            RootId: mapping.Id.ToString(),
            Visibility: ShellFolderVisibility.Hidden,
            SiblingsGrouping: ShellFolderSiblingsGrouping.Independent);

        return await _onDemandSyncRootRegistry.TryUnregisterAsync(root).ConfigureAwait(false);
    }
}
