using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Shared.Features;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Setup.SharedWithMe.SharedWithMeItem;

internal sealed class SharedWithMeItemMappingValidationStep
{
    private readonly ILocalFolderValidationStep _localFolderValidationStep;
    private readonly IRemoteSharedWithMeItemValidationStep _remoteItemValidationStep;
    private readonly IFeatureFlagProvider _featureFlagProvider;

    public SharedWithMeItemMappingValidationStep(
        ILocalFolderValidationStep localFolderValidationStep,
        IRemoteSharedWithMeItemValidationStep remoteItemValidationStep,
        IFeatureFlagProvider featureFlagProvider)
    {
        _remoteItemValidationStep = remoteItemValidationStep;
        _localFolderValidationStep = localFolderValidationStep;
        _featureFlagProvider = featureFlagProvider;
    }

    public async Task<MappingErrorCode> ValidateAsync(
        RemoteToLocalMapping mapping,
        IReadOnlySet<string> otherLocalSyncFolders,
        CancellationToken cancellationToken)
    {
        if (mapping.Type is not MappingType.SharedWithMeItem)
        {
            throw new ArgumentException("Mapping type has unexpected value", nameof(mapping));
        }

        if (await _featureFlagProvider.IsEnabledAsync(Feature.DriveSharingDisabled, cancellationToken).ConfigureAwait(false)
            || (!mapping.Remote.IsReadOnly
                && await _featureFlagProvider.IsEnabledAsync(Feature.DriveSharingEditingDisabled, cancellationToken).ConfigureAwait(false)))
        {
            return MappingErrorCode.SharingDisabled;
        }

        cancellationToken.ThrowIfCancellationRequested();

        return mapping.Remote.RootItemType is LinkType.File
            ? await ValidateFileItem(mapping, cancellationToken).ConfigureAwait(false)
            : await ValidateFolderItem(mapping, otherLocalSyncFolders, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MappingErrorCode> ValidateFolderItem(
        RemoteToLocalMapping mapping,
        IReadOnlySet<string> otherLocalSyncFolders,
        CancellationToken cancellationToken)
    {
        var result =
            await ValidateLocalFolderAsync(mapping, otherLocalSyncFolders, cancellationToken).ConfigureAwait(false) ??
            await _remoteItemValidationStep.ValidateAsync(mapping, cancellationToken).ConfigureAwait(false);

        return result ?? MappingErrorCode.None;
    }

    private async Task<MappingErrorCode> ValidateFileItem(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        var result = await _remoteItemValidationStep.ValidateAsync(mapping, cancellationToken).ConfigureAwait(false);

        return result ?? MappingErrorCode.None;
    }

    private async Task<MappingErrorCode?> ValidateLocalFolderAsync(
        RemoteToLocalMapping mapping,
        IReadOnlySet<string> otherLocalSyncFolders,
        CancellationToken cancellationToken)
    {
        var result = await _localFolderValidationStep.ValidateAsync(mapping, otherLocalSyncFolders, cancellationToken).ConfigureAwait(false);

        return result is not MappingErrorCode.None ? result : null;
    }
}
