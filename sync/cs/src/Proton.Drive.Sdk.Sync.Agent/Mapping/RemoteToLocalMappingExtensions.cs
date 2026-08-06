using Proton.Drive.Sdk.Sync.Agent.Settings;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping;

internal static class RemoteToLocalMappingExtensions
{
    public static bool IsStorageOptimizationPending(this RemoteToLocalMapping mapping)
    {
        return mapping.Local.StorageOptimization?.Status is StorageOptimizationStatus.Pending;
    }

    public static bool IsOrCouldBeConvertedToOnDemand(this RemoteToLocalMapping mapping)
    {
        return mapping.SyncMethod is SyncMethod.OnDemand || mapping.Local.StorageOptimization is not null;
    }

    public static void SetEnablingOnDemandSyncSucceeded(this RemoteToLocalMapping mapping)
    {
        if (mapping.SyncMethod is SyncMethod.OnDemand)
        {
            return;
        }

        mapping.SyncMethod = SyncMethod.OnDemand;
        mapping.IsDirty = true;
    }

    public static void SetEnablingOnDemandSyncFailed(this RemoteToLocalMapping mapping, StorageOptimizationErrorCode errorCode, string? conflictingProviderName = null)
    {
        if (!mapping.IsStorageOptimizationPending())
        {
            return;
        }

        mapping.Local.StorageOptimization ??= new StorageOptimizationState();
        mapping.Local.StorageOptimization.IsEnabled = false;
        mapping.Local.StorageOptimization.Status = StorageOptimizationStatus.Failed;
        mapping.Local.StorageOptimization.ErrorCode = errorCode;
        mapping.Local.StorageOptimization.ConflictingProviderName = conflictingProviderName;
        mapping.IsDirty = true;
    }

    public static void SetStorageOptimizationSucceeded(this RemoteToLocalMapping mapping)
    {
        if (!mapping.IsStorageOptimizationPending())
        {
            return;
        }

        mapping.Local.StorageOptimization ??= new StorageOptimizationState();
        mapping.Local.StorageOptimization.Status = StorageOptimizationStatus.Succeeded;
        mapping.Local.StorageOptimization.ErrorCode = StorageOptimizationErrorCode.None;
        mapping.IsDirty = true;
    }

    public static void SetStorageOptimizationFailed(this RemoteToLocalMapping mapping)
    {
        if (!mapping.IsStorageOptimizationPending())
        {
            return;
        }

        mapping.Local.StorageOptimization ??= new StorageOptimizationState();
        mapping.Local.StorageOptimization.IsEnabled = !mapping.Local.StorageOptimization.IsEnabled;
        mapping.Local.StorageOptimization.Status = StorageOptimizationStatus.Failed;
        mapping.Local.StorageOptimization.ErrorCode = StorageOptimizationErrorCode.Unknown;
        mapping.IsDirty = true;
    }
}
