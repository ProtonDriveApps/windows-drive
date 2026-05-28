using System.ComponentModel;
using ProtonDrive.App.Mapping.Setup.CloudFiles;
using ProtonDrive.App.Mapping.Setup.ForeignDevices;
using ProtonDrive.App.Mapping.Setup.HostDeviceFolders;
using ProtonDrive.App.Mapping.Setup.SharedWithMe.SharedWithMeItem;
using ProtonDrive.App.Mapping.Setup.SharedWithMe.SharedWithMeRootFolder;
using ProtonDrive.App.Settings;

namespace ProtonDrive.App.Mapping.Setup;

internal sealed class MappingFoldersSetupDispatcher
{
    private readonly CloudFilesMappingFoldersSetupStep _cloudFilesMappingStep;
    private readonly HostDeviceFolderMappingFoldersSetupStep _hostDeviceFolderMappingStep;
    private readonly ForeignDeviceMappingFoldersSetupStep _foreignDeviceMappingStep;
    private readonly SharedWithMeRootFolderMappingFoldersSetupStep _sharedWithMeRootFolderMappingStep;
    private readonly SharedWithMeItemMappingSetupStep _sharedWithMeItemMappingStep;

    public MappingFoldersSetupDispatcher(
        CloudFilesMappingFoldersSetupStep cloudFilesMappingStep,
        HostDeviceFolderMappingFoldersSetupStep hostDeviceFolderMappingStep,
        ForeignDeviceMappingFoldersSetupStep foreignDeviceMappingStep,
        SharedWithMeRootFolderMappingFoldersSetupStep sharedWithMeRootFolderMappingStep,
        SharedWithMeItemMappingSetupStep sharedWithMeItemMappingStep)
    {
        _cloudFilesMappingStep = cloudFilesMappingStep;
        _hostDeviceFolderMappingStep = hostDeviceFolderMappingStep;
        _foreignDeviceMappingStep = foreignDeviceMappingStep;
        _sharedWithMeRootFolderMappingStep = sharedWithMeRootFolderMappingStep;
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
            MappingType.SharedWithMeRootFolder => _sharedWithMeRootFolderMappingStep.SetUpFoldersAsync(mapping, cancellationToken),
            MappingType.SharedWithMeItem => _sharedWithMeItemMappingStep.SetUpFoldersAsync(mapping, cancellationToken),
            _ => throw new InvalidEnumArgumentException(nameof(mapping.Type), (int)mapping.Type, typeof(MappingType)),
        };
    }
}
