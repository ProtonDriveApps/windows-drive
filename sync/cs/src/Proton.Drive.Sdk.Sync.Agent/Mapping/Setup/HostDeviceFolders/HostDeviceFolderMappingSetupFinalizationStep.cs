using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.OnDemand;
using Proton.Drive.Shared.Telemetry;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Setup.HostDeviceFolders;

internal sealed class HostDeviceFolderMappingSetupFinalizationStep
{
    private readonly OnDemandSyncEligibilityValidator _onDemandSyncEligibilityValidator;
    private readonly IOnDemandSyncRootRegistry _onDemandSyncRootRegistry;
    private readonly ILocalStorageOptimizationStep _storageOptimizationStep;
    private readonly IErrorCounter _errorCounter;
    private readonly ILogger<HostDeviceFolderMappingSetupFinalizationStep> _logger;

    public HostDeviceFolderMappingSetupFinalizationStep(
        OnDemandSyncEligibilityValidator onDemandSyncEligibilityValidator,
        IOnDemandSyncRootRegistry onDemandSyncRootRegistry,
        ILocalStorageOptimizationStep storageOptimizationStep,
        IErrorCounter errorCounter,
        ILogger<HostDeviceFolderMappingSetupFinalizationStep> logger)
    {
        _onDemandSyncEligibilityValidator = onDemandSyncEligibilityValidator;
        _onDemandSyncRootRegistry = onDemandSyncRootRegistry;
        _storageOptimizationStep = storageOptimizationStep;
        _errorCounter = errorCounter;
        _logger = logger;
    }

    public async Task<MappingErrorCode> FinishSetupAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        if (mapping.Type is not MappingType.HostDeviceFolder)
        {
            throw new ArgumentException("Mapping type has unexpected value", nameof(mapping));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Upon requesting to optimize storage, the mapping sync method can still be classic
        if (mapping.SyncMethod is not SyncMethod.OnDemand && !mapping.IsStorageOptimizationPending())
        {
            return MappingErrorCode.None;
        }

        if (mapping.SyncMethod is not SyncMethod.OnDemand && mapping.IsStorageOptimizationPending() &&
            ValidateOnDemandSyncEligibility(mapping) is { } validationResult)
        {
            _logger.LogWarning("The local sync folder is not eligible for on-demand sync: {ErrorCode}", validationResult);

            mapping.SetEnablingOnDemandSyncFailed(validationResult);

            _errorCounter.Add(ErrorScope.StorageOptimization, new LocalStorageOptimizationException(mapping));

            return MappingErrorCode.None;
        }

        if (await TryAddOnDemandSyncRootAsync(mapping).ConfigureAwait(false) is { } resultInfo)
        {
            if (mapping.SyncMethod is SyncMethod.OnDemand)
            {
                return resultInfo.ErrorCode;
            }

            var errorInfo = ToStorageOptimizationErrorInfo(resultInfo.ErrorCode, resultInfo.ConflictingProviderName);

            mapping.SetEnablingOnDemandSyncFailed(errorInfo.ErrorCode, errorInfo.ConflictingProviderName);

            _errorCounter.Add(ErrorScope.StorageOptimization, new LocalStorageOptimizationException(mapping));

            return MappingErrorCode.None;
        }

        mapping.SetEnablingOnDemandSyncSucceeded();

        OptimizeStorage(mapping);

        return MappingErrorCode.None;
    }

    private static (StorageOptimizationErrorCode ErrorCode, string? ConflictingProviderName) ToStorageOptimizationErrorInfo(MappingErrorCode errorCode, string? conflictingProviderName)
    {
        return errorCode switch
        {
            MappingErrorCode.ConflictingOnDemandSyncRootExists => (StorageOptimizationErrorCode.ConflictingOnDemandSyncRootExists, conflictingProviderName),
            MappingErrorCode.ConflictingDescendantOnDemandSyncRootExists => (StorageOptimizationErrorCode.ConflictingDescendantOnDemandSyncRootExists, conflictingProviderName),
            _ => (StorageOptimizationErrorCode.Unknown, null),
        };
    }

    private StorageOptimizationErrorCode? ValidateOnDemandSyncEligibility(RemoteToLocalMapping mapping)
    {
        return _onDemandSyncEligibilityValidator.Validate(mapping.Local.Path);
    }

    private Task<MappingErrorInfo?> TryAddOnDemandSyncRootAsync(RemoteToLocalMapping mapping)
    {
        return _onDemandSyncRootRegistry.TryAddOnDemandSyncRootAsync(mapping);
    }

    private void OptimizeStorage(RemoteToLocalMapping mapping)
    {
        var errorCode = _storageOptimizationStep.Execute(mapping);

        if (errorCode is null)
        {
            mapping.SetStorageOptimizationSucceeded();
        }
        else
        {
            mapping.SetStorageOptimizationFailed();

            _errorCounter.Add(ErrorScope.StorageOptimization, new LocalStorageOptimizationException(mapping));
        }
    }
}
