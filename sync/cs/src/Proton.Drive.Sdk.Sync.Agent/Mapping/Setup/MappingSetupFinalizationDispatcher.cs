using System.ComponentModel;
using Proton.Drive.Sdk.Sync.Agent.Mapping.Setup.CloudFiles;
using Proton.Drive.Sdk.Sync.Agent.Mapping.Setup.ForeignDevices;
using Proton.Drive.Sdk.Sync.Agent.Mapping.Setup.HostDeviceFolders;
using Proton.Drive.Sdk.Sync.Agent.Mapping.Setup.SharedWithMe.SharedWithMeItem;
using Proton.Drive.Sdk.Sync.Agent.Mapping.Setup.SharedWithMe.SharedWithMeRootFolder;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Setup;

internal sealed class MappingSetupFinalizationDispatcher
{
    private readonly HostDeviceFolderMappingSetupFinalizationStep _hostDeviceFolderMappingStep;
    private readonly CloudFilesMappingSetupFinalizationStep _cloudFilesMappingStep;
    private readonly ForeignDeviceMappingSetupFinalizationStep _foreignDeviceMappingStep;
    private readonly SharedWithMeRootFolderMappingSetupFinalizationStep _sharedWithMeRootFolderMappingStep;
    private readonly SharedWithMeItemMappingSetupFinalizationStep _sharedWithMeItemMappingStep;

    public MappingSetupFinalizationDispatcher(
        HostDeviceFolderMappingSetupFinalizationStep hostDeviceFolderMappingStep,
        CloudFilesMappingSetupFinalizationStep cloudFilesMappingStep,
        ForeignDeviceMappingSetupFinalizationStep foreignDeviceMappingStep,
        SharedWithMeRootFolderMappingSetupFinalizationStep sharedWithMeRootFolderMappingStep,
        SharedWithMeItemMappingSetupFinalizationStep sharedWithMeItemMappingStep)
    {
        _hostDeviceFolderMappingStep = hostDeviceFolderMappingStep;
        _cloudFilesMappingStep = cloudFilesMappingStep;
        _foreignDeviceMappingStep = foreignDeviceMappingStep;
        _sharedWithMeRootFolderMappingStep = sharedWithMeRootFolderMappingStep;
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
            MappingType.HostDeviceFolder => _hostDeviceFolderMappingStep.FinishSetupAsync(mapping, cancellationToken),
            MappingType.ForeignDevice => _foreignDeviceMappingStep.FinishSetupAsync(mapping, cancellationToken),
            MappingType.SharedWithMeRootFolder => _sharedWithMeRootFolderMappingStep.FinishSetupAsync(mapping, cancellationToken),
            MappingType.SharedWithMeItem => _sharedWithMeItemMappingStep.FinishSetupAsync(mapping, cancellationToken),
            _ => throw new InvalidEnumArgumentException(nameof(mapping.Type), (int)mapping.Type, typeof(MappingType)),
        };
    }
}
