using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.App.Mapping.Setup.CloudFiles;
using ProtonDrive.App.Mapping.Setup.ForeignDevices;
using ProtonDrive.App.Mapping.Setup.HostDeviceFolders;
using ProtonDrive.App.Mapping.Setup.SharedWithMe.SharedWithMeItem;
using ProtonDrive.App.Mapping.Setup.SharedWithMe.SharedWithMeItemsFolder;
using ProtonDrive.App.Settings;

namespace ProtonDrive.App.Mapping.Setup;

internal sealed class MappingFoldersSetupDispatcher
{
    private readonly CloudFilesMappingFoldersSetupStep _cloudFilesMappingStep;
    private readonly HostDeviceFolderMappingFoldersSetupStep _hostDeviceFolderMappingStep;
    private readonly ForeignDeviceMappingFoldersSetupStep _foreignDeviceMappingStep;
    private readonly SharedWithMeItemsFolderMappingFoldersSetupStep _sharedWithMeItemsFolderMappingStep;
    private readonly SharedWithMeItemMappingSetupStep _sharedWithMeItemMappingStep;

    public MappingFoldersSetupDispatcher(
        CloudFilesMappingFoldersSetupStep cloudFilesMappingStep,
        HostDeviceFolderMappingFoldersSetupStep hostDeviceFolderMappingStep,
        ForeignDeviceMappingFoldersSetupStep foreignDeviceMappingStep,
        SharedWithMeItemsFolderMappingFoldersSetupStep sharedWithMeItemsFolderMappingStep,
        SharedWithMeItemMappingSetupStep sharedWithMeItemMappingStep)
    {
        _cloudFilesMappingStep = cloudFilesMappingStep;
        _hostDeviceFolderMappingStep = hostDeviceFolderMappingStep;
        _foreignDeviceMappingStep = foreignDeviceMappingStep;
        _sharedWithMeItemsFolderMappingStep = sharedWithMeItemsFolderMappingStep;
        _sharedWithMeItemMappingStep = sharedWithMeItemMappingStep;
    }

    public async Task<MappingState> SetUpFoldersAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        if (mapping.Status == MappingStatus.Complete)
        {
            return MappingState.Success;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var errorCode = await SetUpMappingFoldersAsync(mapping, cancellationToken).ConfigureAwait(false);

        if (errorCode != MappingErrorCode.None)
        {
            return MappingState.Failure(errorCode);
        }

        return MappingState.Success;
    }

    private Task<MappingErrorCode> SetUpMappingFoldersAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        return mapping.Type switch
        {
            MappingType.CloudFiles => _cloudFilesMappingStep.SetUpFoldersAsync(mapping, cancellationToken),
            MappingType.HostDeviceFolder => _hostDeviceFolderMappingStep.SetUpFoldersAsync(mapping, cancellationToken),
            MappingType.ForeignDevice => _foreignDeviceMappingStep.SetUpFoldersAsync(mapping, cancellationToken),
            MappingType.SharedWithMeRootFolder => _sharedWithMeItemsFolderMappingStep.SetUpFoldersAsync(mapping, cancellationToken),
            MappingType.SharedWithMeItem => _sharedWithMeItemMappingStep.SetUpFoldersAsync(mapping, cancellationToken),
            _ => throw new InvalidEnumArgumentException(nameof(mapping.Type), (int)mapping.Type, typeof(MappingType)),
        };
    }
}
