namespace Proton.Drive.Sdk.Sync.Agent.Mapping;

public enum StorageOptimizationErrorCode
{
    None,
    Unknown,
    VolumeNotSupported,
    RemovableVolumeNotSupported,
    NetworkVolumeNotSupported,
    ConflictingOnDemandSyncRootExists,
    ConflictingDescendantOnDemandSyncRootExists,
}
