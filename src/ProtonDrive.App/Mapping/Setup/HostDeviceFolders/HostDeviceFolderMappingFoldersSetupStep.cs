using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Devices;
using ProtonDrive.App.Settings;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.Client;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.Mapping.Setup.HostDeviceFolders;

internal sealed class HostDeviceFolderMappingFoldersSetupStep
{
    private readonly IDeviceService _deviceService;
    private readonly ILocalFolderSetupAssistant _localFolderSetupAssistant;
    private readonly IRemoteFileSystemClientFactory _remoteFileSystemClientFactory;
    private readonly RemoteFolderNameValidator _remoteFolderNameValidator;
    private readonly VolumeIdentityProvider _volumeIdentityProvider;
    private readonly INumberSuffixedNameGenerator _numberSuffixedNameGenerator;
    private readonly ILogger<HostDeviceFolderMappingFoldersSetupStep> _logger;

    public HostDeviceFolderMappingFoldersSetupStep(
        IDeviceService deviceService,
        ILocalFolderSetupAssistant localFolderSetupAssistant,
        IRemoteFileSystemClientFactory remoteFileSystemClientFactory,
        RemoteFolderNameValidator remoteFolderNameValidator,
        VolumeIdentityProvider volumeIdentityProvider,
        INumberSuffixedNameGenerator numberSuffixedNameGenerator,
        ILogger<HostDeviceFolderMappingFoldersSetupStep> logger)
    {
        _deviceService = deviceService;
        _localFolderSetupAssistant = localFolderSetupAssistant;
        _remoteFileSystemClientFactory = remoteFileSystemClientFactory;
        _remoteFolderNameValidator = remoteFolderNameValidator;
        _volumeIdentityProvider = volumeIdentityProvider;
        _numberSuffixedNameGenerator = numberSuffixedNameGenerator;
        _logger = logger;
    }

    public async Task<MappingErrorCode> SetUpFoldersAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        Ensure.IsTrue(mapping.Type is MappingType.HostDeviceFolder, "Mapping type has unexpected value", nameof(mapping));

        return
            SetUpLocalFolder(mapping, cancellationToken) ??
            await SetUpRemoteFolderAsync(mapping, cancellationToken).ConfigureAwait(false) ??
            MappingErrorCode.None;
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
        return _localFolderSetupAssistant.SetUpLocalFolder(mapping, cancellationToken);
    }

    private async Task<MappingErrorCode?> SetUpRemoteFolderAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        var replica = mapping.Remote;

        if (replica.IsSetUp())
        {
            return null;
        }

        var (hostDevice, errorResponseCode) = await _deviceService.SetUpHostDeviceAsync(cancellationToken).ConfigureAwait(false);

        if (hostDevice is null)
        {
            _logger.LogInformation("Setting up remote folder failed");
            return errorResponseCode switch
            {
                ResponseCode.InsufficientDeviceQuota => MappingErrorCode.InsufficientDeviceQuota,
                _ => MappingErrorCode.DriveAccessFailed,
            };
        }

        _logger.LogInformation("Creating host device folder for sync folder mapping {Id} ({Type})", mapping.Id, mapping.Type);

        var folderName = GetFolderNameFromRootFolderPath(mapping.Local.Path);
        var folder = await CreateDeviceFolderAsync(hostDevice, folderName, cancellationToken).ConfigureAwait(false);
        if (folder is null)
        {
            return MappingErrorCode.DriveAccessFailed;
        }

        replica.VolumeId = hostDevice.DataItem.VolumeId;
        replica.ShareId = hostDevice.DataItem.ShareId;
        replica.RootLinkId = folder.Value.Id;
        replica.RootItemName = folder.Value.Name;
        replica.InternalVolumeId = _volumeIdentityProvider.GetRemoteVolumeId(replica.VolumeId);

        return null;
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

            return null;
        }
    }

    private async Task<(string Id, string Name)?> CreateUniqueDeviceFolderAsync(
        Device device,
        string baseName,
        CancellationToken cancellationToken)
    {
        var parameters = new FileSystemClientParameters(device.DataItem.VolumeId, device.DataItem.ShareId);
        var fileSystemClient = _remoteFileSystemClientFactory.CreateClient(parameters);

        foreach (var name in _numberSuffixedNameGenerator.GenerateNames(baseName, NameType.Folder))
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

        return null;
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
            return null;
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

        var folder = await fileSystemClient.CreateDirectoryAsync(folderInfo, cancellationToken).ConfigureAwait(false);

        return folder.Id!;
    }
}
