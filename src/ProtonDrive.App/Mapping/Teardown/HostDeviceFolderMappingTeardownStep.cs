using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Mapping.Setup.HostDeviceFolders;
using ProtonDrive.App.Settings;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.Mapping.Teardown;

internal sealed class HostDeviceFolderMappingTeardownStep
{
    private readonly ILocalSpecialSubfoldersDeletionStep _specialFoldersDeletion;
    private readonly Func<FileSystemClientParameters, IFileSystemClient<string>> _remoteFileSystemClientFactory;
    private readonly ILogger<HostDeviceFolderMappingFoldersSetupStep> _logger;

    public HostDeviceFolderMappingTeardownStep(
        ILocalSpecialSubfoldersDeletionStep specialFoldersDeletion,
        Func<FileSystemClientParameters, IFileSystemClient<string>> remoteFileSystemClientFactory,
        ILogger<HostDeviceFolderMappingFoldersSetupStep> logger)
    {
        _specialFoldersDeletion = specialFoldersDeletion;
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

        var result =
            DeleteSpecialSubfolders(mapping) ??
            await DeleteHostDeviceFolder(mapping.Remote, cancellationToken).ConfigureAwait(false);

        return result ?? MappingErrorCode.None;
    }

    private MappingErrorCode? DeleteSpecialSubfolders(RemoteToLocalMapping mapping)
    {
        _specialFoldersDeletion.DeleteSpecialSubfolders(mapping.Local.RootFolderPath);

        return default;
    }

    private async Task<MappingErrorCode?> DeleteHostDeviceFolder(RemoteReplica replica, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(replica.RootLinkId) || string.IsNullOrEmpty(replica.ShareId))
        {
            return default;
        }

        await DeleteDeviceFolderAsync(replica.VolumeId ?? string.Empty, replica.ShareId, replica.RootLinkId, cancellationToken).ConfigureAwait(false);

        return default;
    }

    private async Task DeleteDeviceFolderAsync(string volumeId, string shareId, string id, CancellationToken cancellationToken)
    {
        var parameters = new FileSystemClientParameters(volumeId, shareId);
        var fileSystemClient = _remoteFileSystemClientFactory.Invoke(parameters);

        var folderInfo = NodeInfo<string>.Directory().WithId(id);

        try
        {
            await fileSystemClient.Delete(folderInfo, cancellationToken).ConfigureAwait(false);
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

            return;
        }

        _logger.LogInformation("Moved to trash remote host device folder with ID={Id}", id);
    }
}
