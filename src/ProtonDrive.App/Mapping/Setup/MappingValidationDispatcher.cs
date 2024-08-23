using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.App.Mapping.Setup.CloudFiles;
using ProtonDrive.App.Mapping.Setup.ForeignDevices;
using ProtonDrive.App.Mapping.Setup.HostDeviceFolders;
using ProtonDrive.App.Mapping.Setup.SharedWithMe.SharedWithMeItem;
using ProtonDrive.App.Settings;

namespace ProtonDrive.App.Mapping.Setup;

internal sealed class MappingValidationDispatcher
{
    private readonly CloudFilesMappingFolderValidationStep _cloudFilesMappingStep;
    private readonly HostDeviceFolderMappingFolderValidationStep _hostDeviceFolderMappingStep;
    private readonly ForeignDeviceMappingFolderValidationStep _foreignDeviceMappingStep;
    private readonly SharedWithMeItemMappingValidationStep _sharedWithMeItemMappingStep;

    public MappingValidationDispatcher(
        CloudFilesMappingFolderValidationStep cloudFilesMappingStep,
        HostDeviceFolderMappingFolderValidationStep hostDeviceFolderMappingStep,
        ForeignDeviceMappingFolderValidationStep foreignDeviceMappingStep,
        SharedWithMeItemMappingValidationStep sharedWithMeItemMappingStep)
    {
        _cloudFilesMappingStep = cloudFilesMappingStep;
        _hostDeviceFolderMappingStep = hostDeviceFolderMappingStep;
        _foreignDeviceMappingStep = foreignDeviceMappingStep;
        _sharedWithMeItemMappingStep = sharedWithMeItemMappingStep;
    }

    public async Task<MappingState> ValidateAsync(RemoteToLocalMapping mapping, IReadOnlySet<string> otherLocalSyncFolders, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var errorCode = await ValidateMappingAsync(mapping, otherLocalSyncFolders, cancellationToken).ConfigureAwait(false);

        if (errorCode != MappingErrorCode.None)
        {
            return MappingState.Failure(errorCode);
        }

        return MappingState.Success;
    }

    private Task<MappingErrorCode> ValidateMappingAsync(RemoteToLocalMapping mapping, IReadOnlySet<string> otherLocalSyncFolders, CancellationToken cancellationToken)
    {
        return mapping.Type switch
        {
            MappingType.CloudFiles => _cloudFilesMappingStep.ValidateAsync(mapping, otherLocalSyncFolders, cancellationToken),
            MappingType.HostDeviceFolder => _hostDeviceFolderMappingStep.ValidateAsync(mapping, otherLocalSyncFolders, cancellationToken),
            MappingType.ForeignDevice => _foreignDeviceMappingStep.ValidateAsync(mapping, otherLocalSyncFolders, cancellationToken),
            MappingType.SharedWithMeRootFolder => Task.FromResult(MappingErrorCode.None),
            MappingType.SharedWithMeItem => _sharedWithMeItemMappingStep.ValidateAsync(mapping, otherLocalSyncFolders, cancellationToken),
            _ => throw new InvalidEnumArgumentException(nameof(mapping.Type), (int)mapping.Type, typeof(MappingType)),
        };
    }
}
