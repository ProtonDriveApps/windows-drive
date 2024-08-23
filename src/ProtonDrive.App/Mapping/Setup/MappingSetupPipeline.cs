using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Settings;

namespace ProtonDrive.App.Mapping.Setup;

internal sealed class MappingSetupPipeline : IMappingSetupPipeline
{
    private readonly MappingValidationDispatcher _validation;
    private readonly MappingFoldersSetupDispatcher _foldersSetup;
    private readonly MappingSetupFinalizationDispatcher _setupFinish;
    private readonly ILogger<MappingSetupPipeline> _logger;

    public MappingSetupPipeline(
        MappingValidationDispatcher validation,
        MappingFoldersSetupDispatcher foldersSetup,
        MappingSetupFinalizationDispatcher setupFinish,
        ILogger<MappingSetupPipeline> logger)
    {
        _validation = validation;
        _foldersSetup = foldersSetup;
        _setupFinish = setupFinish;
        _logger = logger;
    }

    public async Task<MappingState> SetUpAsync(
        RemoteToLocalMapping mapping,
        IReadOnlySet<string> otherLocalSyncFolders,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Setting up sync folder mapping {Id} ({Type})", mapping.Id, mapping.Type);

        var result = await SetUpMappingAsync(mapping, otherLocalSyncFolders, cancellationToken).ConfigureAwait(false);

        if (result.Status is not MappingSetupStatus.Succeeded)
        {
            _logger.LogInformation("Setting up sync folder mapping {Id} ({Type}) failed", mapping.Id, mapping.Type);

            return result;
        }

        _logger.LogInformation("Setting up sync folder mapping {Id} ({Type}) succeeded", mapping.Id, mapping.Type);

        // Setup succeeded, mapping is complete
        mapping.Status = MappingStatus.Complete;

        return result;
    }

    private async Task<MappingState> SetUpMappingAsync(
        RemoteToLocalMapping mapping,
        IReadOnlySet<string> otherLocalSyncFolders,
        CancellationToken cancellationToken)
    {
        // Validation
        var result = await _validation.ValidateAsync(mapping, otherLocalSyncFolders, cancellationToken).ConfigureAwait(false);

        if (result.Status is not MappingSetupStatus.Succeeded)
        {
            return result;
        }

        // Folders setup
        result = await _foldersSetup.SetUpFoldersAsync(mapping, cancellationToken).ConfigureAwait(false);

        if (result.Status is not MappingSetupStatus.Succeeded)
        {
            return result;
        }

        // Finishing
        result = await _setupFinish.FinishSetupAsync(mapping, cancellationToken).ConfigureAwait(false);

        return result;
    }
}
