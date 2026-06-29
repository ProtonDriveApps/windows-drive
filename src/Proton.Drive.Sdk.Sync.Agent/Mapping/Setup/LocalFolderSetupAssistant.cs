using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Setup;

internal sealed class LocalFolderSetupAssistant : ILocalFolderSetupAssistant
{
    private readonly ILocalFolderService _localFolderService;
    private readonly LocalFolderIdentityValidator _localFolderIdentityValidator;
    private readonly VolumeIdentityProvider _volumeIdentityProvider;
    private readonly ILogger<LocalFolderSetupAssistant> _logger;

    public LocalFolderSetupAssistant(
        ILocalFolderService localFolderService,
        LocalFolderIdentityValidator localFolderIdentityValidator,
        VolumeIdentityProvider volumeIdentityProvider,
        ILogger<LocalFolderSetupAssistant> logger)
    {
        _localFolderService = localFolderService;
        _localFolderIdentityValidator = localFolderIdentityValidator;
        _volumeIdentityProvider = volumeIdentityProvider;
        _logger = logger;
    }

    public MappingErrorCode? SetUpLocalFolder(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        var replica = mapping.Local;

        if (replica.IsSetUp())
        {
            // Already set up
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!_localFolderService.TryGetFolderInfo(replica.Path, FileShare.ReadWrite, out var rootFolder))
        {
            _logger.LogWarning("Failed to access local folder");
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        if (rootFolder == null)
        {
            _logger.LogWarning("The local folder does not exist");
            return MappingErrorCode.LocalFolderDoesNotExist;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var result = _localFolderIdentityValidator.ValidateFolderIdentity(rootFolder, replica, mapping.Remote.RootItemType);
        if (result is not null)
        {
            return result;
        }

        replica.RootFolderId = rootFolder.Id;
        replica.VolumeSerialNumber = rootFolder.VolumeInfo.VolumeSerialNumber;
        replica.InternalVolumeId = _volumeIdentityProvider.GetLocalVolumeId(replica.VolumeSerialNumber);

        return null;
    }
}
