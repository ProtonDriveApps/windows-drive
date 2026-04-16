using ProtonDrive.App.Photos.Import;
using ProtonDrive.App.Settings;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Repository;

namespace ProtonDrive.App.Mapping.Setup.PhotoFolders;

internal sealed class PhotoFolderMappingValidationStep
{
    private readonly IRepository<PhotoImportSettings> _importRepository;
    private readonly IPhotosFeatureStateValidator _photosFeatureStateValidator;
    private readonly ILocalFolderValidationStep _localFolderValidation;
    private readonly IRemotePhotoVolumeValidator _remotePhotoVolumeValidator;

    public PhotoFolderMappingValidationStep(
        IRepository<PhotoImportSettings> importRepository,
        IPhotosFeatureStateValidator photosFeatureStateValidator,
        ILocalFolderValidationStep localFolderValidation,
        IRemotePhotoVolumeValidator remotePhotoVolumeValidator)
    {
        _importRepository = importRepository;
        _photosFeatureStateValidator = photosFeatureStateValidator;
        _localFolderValidation = localFolderValidation;
        _remotePhotoVolumeValidator = remotePhotoVolumeValidator;
    }

    public async Task<MappingErrorCode> ValidateAsync(
        RemoteToLocalMapping mapping,
        IReadOnlySet<string> otherLocalSyncFolders,
        CancellationToken cancellationToken)
    {
        Ensure.IsTrue(mapping.IsPhotoFolderMapping(), "Mapping type has unexpected value", nameof(mapping));

        return
            ValidateImportFolderStatus(mapping) ??
            ValidatePhotosFeatureState() ??
            await ValidateLocalFolderAsync(mapping, otherLocalSyncFolders, cancellationToken).ConfigureAwait(false) ??
            await ValidateRemoteFolderAsync(mapping.Remote, cancellationToken).ConfigureAwait(false) ??
            MappingErrorCode.None;
    }

    private MappingErrorCode? ValidateImportFolderStatus(RemoteToLocalMapping mapping)
    {
        var photoImportFolders = _importRepository.Get()?.Folders ?? [];
        var photoImportFolderStatus = photoImportFolders.FirstOrDefault(x => x.MappingId == mapping.Id)?.Status;

        return photoImportFolderStatus switch
        {
            PhotoImportFolderStatus.Succeeded => MappingErrorCode.None, // We skip validation of already imported folders
            _ => null,
        };
    }

    private MappingErrorCode? ValidatePhotosFeatureState()
    {
        return _photosFeatureStateValidator.Validate();
    }

    private async Task<MappingErrorCode?> ValidateLocalFolderAsync(
        RemoteToLocalMapping mapping,
        IReadOnlySet<string> otherLocalSyncFolders,
        CancellationToken cancellationToken)
    {
        var result = await _localFolderValidation.ValidateAsync(mapping, otherLocalSyncFolders, cancellationToken).ConfigureAwait(false);

        return result is not MappingErrorCode.None ? result : null;
    }

    private async Task<MappingErrorCode?> ValidateRemoteFolderAsync(RemoteReplica replica, CancellationToken cancellationToken)
    {
        if (!replica.IsSetUp())
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        return
            ValidatePhotoVolumeIdentity(replica) ??
            await CheckPhotoVolumeAccessibilityAsync(replica, cancellationToken).ConfigureAwait(false);
    }

    private MappingErrorCode? ValidatePhotoVolumeIdentity(RemoteReplica replica)
    {
        return _remotePhotoVolumeValidator.ValidateIdentity(replica);
    }

    private Task<MappingErrorCode?> CheckPhotoVolumeAccessibilityAsync(RemoteReplica replica, CancellationToken cancellationToken)
    {
        return _remotePhotoVolumeValidator.CheckAccessibilityAsync(replica, cancellationToken);
    }
}
