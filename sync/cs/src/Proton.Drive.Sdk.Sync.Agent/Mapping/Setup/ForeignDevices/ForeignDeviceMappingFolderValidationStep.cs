using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Setup.ForeignDevices;

internal sealed class ForeignDeviceMappingFolderValidationStep
{
    private readonly ILocalFolderValidationStep _localFolderValidationStep;
    private readonly IRemoteFolderValidationStep _remoteFolderValidationStep;

    public ForeignDeviceMappingFolderValidationStep(
        ILocalFolderValidationStep localFolderValidationStep,
        IRemoteFolderValidationStep remoteFolderValidationStep)
    {
        _localFolderValidationStep = localFolderValidationStep;
        _remoteFolderValidationStep = remoteFolderValidationStep;
    }

    public async Task<MappingErrorCode> ValidateAsync(
        RemoteToLocalMapping mapping,
        IReadOnlySet<string> otherLocalSyncFolders,
        CancellationToken cancellationToken)
    {
        if (mapping.Type is not MappingType.ForeignDevice)
        {
            throw new ArgumentException("Mapping type has unexpected value", nameof(mapping));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var result =
            await ValidateLocalFolderAsync(mapping, otherLocalSyncFolders, cancellationToken).ConfigureAwait(false) ??
            await ValidateRemoteFolderAsync(mapping, cancellationToken).ConfigureAwait(false);

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

    private Task<MappingErrorCode?> ValidateRemoteFolderAsync(
        RemoteToLocalMapping mapping,
        CancellationToken cancellationToken)
    {
        return _remoteFolderValidationStep.ValidateAsync(mapping, cancellationToken);
    }
}
