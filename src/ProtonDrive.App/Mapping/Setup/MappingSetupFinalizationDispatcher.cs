using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.App.Mapping.Setup.CloudFiles;
using ProtonDrive.App.Mapping.Setup.ForeignDevices;
using ProtonDrive.App.Mapping.Setup.SharedWithMe.SharedWithMeItem;
using ProtonDrive.App.Mapping.Setup.SharedWithMe.SharedWithMeItemsFolder;
using ProtonDrive.App.Settings;

namespace ProtonDrive.App.Mapping.Setup;

internal sealed class MappingSetupFinalizationDispatcher
{
    private readonly CloudFilesMappingSetupFinalizationStep _cloudFilesMappingStep;
    private readonly ForeignDeviceMappingSetupFinalizationStep _foreignDeviceMappingStep;
    private readonly SharedWithMeItemsFolderMappingSetupFinalizationStep _sharedWithMeItemsFolderMappingStep;
    private readonly SharedWithMeItemMappingSetupFinalizationStep _sharedWithMeItemMappingStep;

    public MappingSetupFinalizationDispatcher(
        CloudFilesMappingSetupFinalizationStep cloudFilesMappingStep,
        ForeignDeviceMappingSetupFinalizationStep foreignDeviceMappingStep,
        SharedWithMeItemsFolderMappingSetupFinalizationStep sharedWithMeItemFolderMappingStep,
        SharedWithMeItemMappingSetupFinalizationStep sharedWithMeItemMappingStep)
    {
        _cloudFilesMappingStep = cloudFilesMappingStep;
        _foreignDeviceMappingStep = foreignDeviceMappingStep;
        _sharedWithMeItemsFolderMappingStep = sharedWithMeItemFolderMappingStep;
        _sharedWithMeItemMappingStep = sharedWithMeItemMappingStep;
    }

    public async Task<MappingState> FinishSetupAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var errorCode = await FinishMappingSetupAsync(mapping, cancellationToken).ConfigureAwait(false);

        if (errorCode != MappingErrorCode.None)
        {
            return MappingState.Failure(errorCode);
        }

        return MappingState.Success;
    }

    private Task<MappingErrorCode> FinishMappingSetupAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        return mapping.Type switch
        {
            MappingType.CloudFiles => _cloudFilesMappingStep.FinishSetupAsync(mapping, cancellationToken),
            MappingType.HostDeviceFolder => Task.FromResult(MappingErrorCode.None),
            MappingType.ForeignDevice => _foreignDeviceMappingStep.FinishSetupAsync(mapping, cancellationToken),
            MappingType.SharedWithMeRootFolder => _sharedWithMeItemsFolderMappingStep.FinishSetupAsync(mapping, cancellationToken),
            MappingType.SharedWithMeItem => _sharedWithMeItemMappingStep.FinishSetupAsync(mapping, cancellationToken),
            _ => throw new InvalidEnumArgumentException(nameof(mapping.Type), (int)mapping.Type, typeof(MappingType)),
        };
    }
}
