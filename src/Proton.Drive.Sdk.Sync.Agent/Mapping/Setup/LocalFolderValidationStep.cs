using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Setup;

internal sealed class LocalFolderValidationStep : ILocalFolderValidationStep
{
    private readonly ILocalFolderService _localFolderService;
    private readonly ILocalSyncFolderValidator _syncFolderValidator;
    private readonly LocalFolderIdentityValidator _localFolderIdentityValidator;
    private readonly LocalFolderDivergedIdentityHandler _divergedIdentityHandler;
    private readonly VolumeIdentityProvider _volumeIdentityProvider;
    private readonly ILogger<LocalFolderValidationStep> _logger;

    public LocalFolderValidationStep(
        ILocalFolderService localFolderService,
        ILocalSyncFolderValidator syncFolderValidator,
        LocalFolderIdentityValidator localFolderIdentityValidator,
        LocalFolderDivergedIdentityHandler divergedIdentityHandler,
        VolumeIdentityProvider volumeIdentityProvider,
        ILogger<LocalFolderValidationStep> logger)
    {
        _localFolderService = localFolderService;
        _syncFolderValidator = syncFolderValidator;
        _localFolderIdentityValidator = localFolderIdentityValidator;
        _divergedIdentityHandler = divergedIdentityHandler;
        _volumeIdentityProvider = volumeIdentityProvider;
        _logger = logger;
    }

    public Task<MappingErrorCode> ValidateAsync(RemoteToLocalMapping mapping, IReadOnlySet<string> otherLocalSyncFolders, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValidateFolderItem(mapping, otherLocalSyncFolders);
    }

    private Task<MappingErrorCode> ValidateFolderItem(RemoteToLocalMapping mapping, IReadOnlySet<string> otherLocalSyncFolders)
    {
        var shouldFolderExist =
            mapping.Status is MappingStatus.Complete
            || mapping.Local.RootFolderId != 0
            || mapping.Type is MappingType.HostDeviceFolder;

        if (!shouldFolderExist)
        {
            return Task.FromResult(MappingErrorCode.None);
        }

        if (string.IsNullOrEmpty(mapping.Local.Path))
        {
            throw new ArgumentException("Local folder path must be specified");
        }

        var result =
            ValidateDriveOfNewFolder(mapping.Local.Path, mapping.Status is MappingStatus.New) ??
            ValidateFolder(mapping.Local.Path, otherLocalSyncFolders) ??
            ValidateFolderIdentity(mapping) ??
            MappingErrorCode.None;

        return Task.FromResult(result);
    }

    private MappingErrorCode? ValidateDriveOfNewFolder(string path, bool isNew)
    {
        if (!isNew)
        {
            // We do not validate drive of already set up local folders, because in the past we
            // allowed users to add network drives, that are currently forbidden. Their
            // validation would fail.
            return null;
        }

        var result = _syncFolderValidator.ValidateDrive(path);

        if (result is null)
        {
            return null;
        }

        _logger.LogWarning("The local sync folder validation failed with error {ErrorCode}", result);

        return (MappingErrorCode)result;
    }

    private MappingErrorCode? ValidateFolder(string path, IReadOnlySet<string> otherLocalSyncFolders)
    {
        var result =
            _syncFolderValidator.ValidatePath(path, otherLocalSyncFolders) ??
            _syncFolderValidator.ValidateFolder(path, shouldBeEmpty: false);

        if (result is null)
        {
            return null;
        }

        _logger.LogWarning("The local sync folder validation failed with error {ErrorCode}", result);

        return (MappingErrorCode)result;
    }

    private MappingErrorCode? ValidateFolderIdentity(RemoteToLocalMapping mapping)
    {
        var replica = mapping.Local;

        if (mapping.Status is MappingStatus.Complete && replica.RootFolderId == 0)
        {
            _logger.LogError("Local sync folder identity not specified");
            return MappingErrorCode.LocalFolderDoesNotExist;
        }

        if (!TryGetLocalFolderInfo(replica.Path, out var rootFolderInfo))
        {
            _logger.LogWarning("Failed to access local sync folder");
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        if (rootFolderInfo == null)
        {
            _logger.LogWarning("The local sync folder does not exist");
            return MappingErrorCode.LocalFolderDoesNotExist;
        }

        if (_localFolderIdentityValidator.ValidateFolderIdentity(rootFolderInfo, replica, mapping.Remote.RootItemType) is { } result)
        {
            if (result is not MappingErrorCode.LocalFolderDiverged)
            {
                return result;
            }

            if (!_divergedIdentityHandler.TryAcceptDivergedIdentity(rootFolderInfo, mapping))
            {
                return result;
            }

            mapping.IsDirty = true;
        }

        AddMissingVolumeInfo(mapping.Local, rootFolderInfo);

        return null;
    }

    private void AddMissingVolumeInfo(LocalReplica replica, LocalFolderInfo folderInfo)
    {
        if (replica.VolumeSerialNumber == 0)
        {
            replica.VolumeSerialNumber = folderInfo.VolumeInfo.VolumeSerialNumber;
        }

        if (replica.InternalVolumeId == 0)
        {
            replica.InternalVolumeId = _volumeIdentityProvider.GetLocalVolumeId(replica.VolumeSerialNumber);
        }
    }

    private bool TryGetLocalFolderInfo(string path, out LocalFolderInfo? folderInfo)
    {
        return _localFolderService.TryGetFolderInfo(path, FileShare.ReadWrite, out folderInfo);
    }
}
