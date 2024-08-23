using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Devices;
using ProtonDrive.App.Settings;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.Mapping.Setup.HostDeviceFolders;

internal sealed class HostDeviceFolderMappingFoldersSetupStep
{
    private readonly IDeviceService _deviceService;
    private readonly ILocalFolderService _localFolderService;
    private readonly Func<FileSystemClientParameters, IFileSystemClient<string>> _remoteFileSystemClientFactory;
    private readonly LocalFolderIdentityValidator _localFolderIdentityValidator;
    private readonly RemoteFolderNameValidator _remoteFolderNameValidator;
    private readonly VolumeIdentityProvider _volumeIdentityProvider;
    private readonly ILogger<HostDeviceFolderMappingFoldersSetupStep> _logger;

    public HostDeviceFolderMappingFoldersSetupStep(
        IDeviceService deviceService,
        ILocalFolderService localFolderService,
        Func<FileSystemClientParameters, IFileSystemClient<string>> remoteFileSystemClientFactory,
        LocalFolderIdentityValidator localFolderIdentityValidator,
        RemoteFolderNameValidator remoteFolderNameValidator,
        VolumeIdentityProvider volumeIdentityProvider,
        ILogger<HostDeviceFolderMappingFoldersSetupStep> logger)
    {
        _deviceService = deviceService;
        _localFolderService = localFolderService;
        _remoteFileSystemClientFactory = remoteFileSystemClientFactory;
        _localFolderIdentityValidator = localFolderIdentityValidator;
        _remoteFolderNameValidator = remoteFolderNameValidator;
        _volumeIdentityProvider = volumeIdentityProvider;
        _logger = logger;
    }

    public async Task<MappingErrorCode> SetUpFoldersAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        if (mapping.Type is not MappingType.HostDeviceFolder)
        {
            throw new ArgumentException("Mapping type has unexpected value", nameof(mapping));
        }

        var result =
            SetUpLocalFolder(mapping, cancellationToken) ??
            await SetUpRemoteFolderAsync(mapping, cancellationToken).ConfigureAwait(false);

        return result ?? MappingErrorCode.None;
    }

    private static string GetFolderNameFromRootFolderPath(string path)
    {
        var folderName = Path.GetFileName(path);

        var isDrivePath = string.IsNullOrEmpty(folderName);

        if (!isDrivePath)
        {
            return folderName;
        }

        var pathRoot = Path.GetPathRoot(path);

        if (pathRoot is null)
        {
            return string.Empty;
        }

        var driveLetter = new string(pathRoot.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());

        if (TryGetVolumeLabel(pathRoot, out var volumeLabel))
        {
            return $"{driveLetter} ({volumeLabel})";
        }

        return driveLetter;
    }

    private static bool TryGetVolumeLabel(string? pathRoot, [MaybeNullWhen(false)] out string volumeLabel)
    {
        try
        {
            var driveInfo = Array.Find(DriveInfo.GetDrives(), x => x.Name.Equals(pathRoot));

            volumeLabel = driveInfo?.VolumeLabel;
            return !string.IsNullOrEmpty(volumeLabel);
        }
        catch (Exception ex) when (ex.IsFileAccessException())
        {
            volumeLabel = null;
            return false;
        }
    }

    private MappingErrorCode? SetUpLocalFolder(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        var replica = mapping.Local;

        if (replica.RootFolderId != default)
        {
            // Already set up
            return default;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!_localFolderService.TryGetFolderInfo(replica.RootFolderPath, FileShare.ReadWrite, out var rootFolder))
        {
            _logger.LogWarning("Failed to access local sync folder");
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        if (rootFolder == null)
        {
            _logger.LogWarning("The local sync folder does not exist");
            return MappingErrorCode.LocalFolderDoesNotExist;
        }

        var result = _localFolderIdentityValidator.ValidateFolderIdentity(rootFolder, replica, mapping.Remote.RootLinkType);
        if (result is not null)
        {
            return result;
        }

        replica.RootFolderId = rootFolder.Id;
        replica.VolumeSerialNumber = rootFolder.VolumeInfo.VolumeSerialNumber;
        replica.InternalVolumeId = _volumeIdentityProvider.GetLocalVolumeId(replica.VolumeSerialNumber);

        return default;
    }

    private async Task<MappingErrorCode?> SetUpRemoteFolderAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        var replica = mapping.Remote;

        if (!string.IsNullOrEmpty(replica.RootLinkId) && !string.IsNullOrEmpty(replica.ShareId))
        {
            // Already set up
            return default;
        }

        var hostDevice = await _deviceService.SetUpHostDeviceAsync(cancellationToken).ConfigureAwait(false);
        if (hostDevice is null)
        {
            return MappingErrorCode.DriveAccessFailed;
        }

        _logger.LogInformation("Creating host device folder for sync folder mapping {Id} ({Type})", mapping.Id, mapping.Type);

        var folderName = GetFolderNameFromRootFolderPath(mapping.Local.RootFolderPath);
        var folder = await CreateDeviceFolderAsync(hostDevice, folderName, cancellationToken).ConfigureAwait(false);
        if (folder is null)
        {
            return MappingErrorCode.DriveAccessFailed;
        }

        replica.VolumeId = hostDevice.DataItem.VolumeId;
        replica.ShareId = hostDevice.DataItem.ShareId;
        replica.RootLinkId = folder.Value.Id;
        replica.RootFolderName = folder.Value.Name;
        replica.InternalVolumeId = _volumeIdentityProvider.GetRemoteVolumeId(replica.VolumeId);

        return default;
    }

    private async Task<(string Id, string Name)?> CreateDeviceFolderAsync(
        Device device,
        string name,
        CancellationToken cancellationToken)
    {
        Ensure.NotNullOrEmpty(name, nameof(name));

        var nameToLog = _logger.GetSensitiveValueForLogging(name);

        try
        {
            var result = await CreateUniqueDeviceFolderAsync(device, name, cancellationToken).ConfigureAwait(false);

            if (result is null)
            {
                _logger.LogError("Creating host device folder \"{FolderName}\" failed: Unable to generate unique name", nameToLog);
                return result;
            }

            nameToLog = _logger.GetSensitiveValueForLogging(result.Value.Name);
            _logger.LogInformation("Created host device folder \"{FolderName}\" with ID {Id}", nameToLog, result.Value.Id);

            return result;
        }
        catch (FileSystemClientException<string> ex)
        {
            _logger.LogWarning(
                "Creating host device folder \"{FolderName}\" failed: {ErrorMessage}",
                nameToLog,
                ex.CombinedMessage());

            return default;
        }
    }

    private async Task<(string Id, string Name)?> CreateUniqueDeviceFolderAsync(
        Device device,
        string baseName,
        CancellationToken cancellationToken)
    {
        var parameters = new FileSystemClientParameters(device.DataItem.VolumeId, device.DataItem.ShareId);
        var fileSystemClient = _remoteFileSystemClientFactory.Invoke(parameters);
        var nameGenerator = new NumberSuffixedNameGenerator(baseName, NameType.Folder);

        foreach (var name in nameGenerator.GenerateNames())
        {
            if (_remoteFolderNameValidator.IsFolderNameInUse(device.DataItem.ShareId, name))
            {
                continue;
            }

            var id = await TryCreateUniqueFolderAsync(
                    fileSystemClient,
                    device.DataItem.LinkId,
                    name,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrEmpty(id))
            {
                return (id, name);
            }
        }

        return default;
    }

    private async Task<string?> TryCreateUniqueFolderAsync(
        IFileSystemClient<string> fileSystemClient,
        string parentId,
        string name,
        CancellationToken cancellationToken)
    {
        try
        {
            return await CreateFolderAsync(fileSystemClient, parentId, name, cancellationToken).ConfigureAwait(false);
        }
        catch (FileSystemClientException<string> ex) when (ex.ErrorCode == FileSystemErrorCode.DuplicateName)
        {
            return default;
        }
    }

    private async Task<string> CreateFolderAsync(
        IFileSystemClient<string> fileSystemClient,
        string parentId,
        string name,
        CancellationToken cancellationToken)
    {
        var folderInfo = NodeInfo<string>.Directory()
            .WithName(name)
            .WithParentId(parentId);

        var folder = await fileSystemClient.CreateDirectory(folderInfo, cancellationToken).ConfigureAwait(false);

        return folder.Id!;
    }
}
