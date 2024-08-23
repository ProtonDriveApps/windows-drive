using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Settings;

namespace ProtonDrive.App.Mapping.Teardown;

internal sealed class MappingTeardownPipeline : IMappingTeardownPipeline
{
    private readonly CloudFilesMappingTeardownStep _cloudFilesMappingStep;
    private readonly HostDeviceFolderMappingTeardownStep _hostDeviceFolderMappingStep;
    private readonly ForeignDeviceMappingTeardownStep _foreignDeviceMappingStep;
    private readonly SharedWithMeItemMappingTeardownStep _sharedWithMeItemMappingStep;
    private readonly SharedWithMeItemsFolderMappingTeardownStep _sharedWithMeItemsFolderMappingStep;
    private readonly ILogger<MappingTeardownPipeline> _logger;

    public MappingTeardownPipeline(
        CloudFilesMappingTeardownStep cloudFilesMappingStep,
        HostDeviceFolderMappingTeardownStep hostDeviceFolderMappingStep,
        ForeignDeviceMappingTeardownStep foreignDeviceMappingStep,
        SharedWithMeItemMappingTeardownStep sharedWithMeItemMappingStep,
        SharedWithMeItemsFolderMappingTeardownStep sharedWithMeItemsFolderMappingStep,
        ILogger<MappingTeardownPipeline> logger)
    {
        _cloudFilesMappingStep = cloudFilesMappingStep;
        _hostDeviceFolderMappingStep = hostDeviceFolderMappingStep;
        _foreignDeviceMappingStep = foreignDeviceMappingStep;
        _sharedWithMeItemMappingStep = sharedWithMeItemMappingStep;
        _sharedWithMeItemsFolderMappingStep = sharedWithMeItemsFolderMappingStep;
        _logger = logger;
    }

    public async Task<MappingState> TearDownAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Tearing down sync folder mapping {Id} ({Type})", mapping.Id, mapping.Type);

        var errorCode = await TearDownMappingAsync(mapping, cancellationToken).ConfigureAwait(false);

        if (errorCode != MappingErrorCode.None)
        {
            _logger.LogInformation("Tearing down sync folder mapping {Id} ({Type}) failed", mapping.Id, mapping.Type);

            return MappingState.Failure(errorCode);
        }

        _logger.LogInformation("Tearing down sync folder mapping {Id} ({Type}) succeeded", mapping.Id, mapping.Type);

        mapping.Status = MappingStatus.TornDown;

        return MappingState.Success;
    }

    private Task<MappingErrorCode> TearDownMappingAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        return mapping.Type switch
        {
            MappingType.CloudFiles => _cloudFilesMappingStep.TearDownAsync(mapping, cancellationToken),
            MappingType.HostDeviceFolder => _hostDeviceFolderMappingStep.TearDownAsync(mapping, cancellationToken),
            MappingType.ForeignDevice => _foreignDeviceMappingStep.TearDownAsync(mapping, cancellationToken),
            MappingType.SharedWithMeRootFolder => _sharedWithMeItemsFolderMappingStep.TearDownAsync(mapping, cancellationToken),
            MappingType.SharedWithMeItem => _sharedWithMeItemMappingStep.TearDownAsync(mapping, cancellationToken),
            _ => throw new InvalidEnumArgumentException(nameof(mapping.Type), (int)mapping.Type, typeof(MappingType)),
        };
    }
}
