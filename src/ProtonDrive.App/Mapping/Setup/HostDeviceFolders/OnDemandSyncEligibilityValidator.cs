using ProtonDrive.Sync.Shared.FileSystem.Integration;

namespace ProtonDrive.App.Mapping.Setup.HostDeviceFolders;

internal sealed class OnDemandSyncEligibilityValidator
{
    private readonly ILocalVolumeInfoProvider _volumeInfoProvider;

    public OnDemandSyncEligibilityValidator(ILocalVolumeInfoProvider volumeInfoProvider)
    {
        _volumeInfoProvider = volumeInfoProvider;
    }

    public StorageOptimizationErrorCode? Validate(string folderPath)
    {
        return ValidateDriveType(folderPath);
    }

    private StorageOptimizationErrorCode? ValidateDriveType(string path)
    {
        var driveType = _volumeInfoProvider.GetDriveType(path);

        // Folders on fixed disks are allowed to be synced on-demand, other types of volumes are prevented,
        // like removable storage devices, including USB flash drives. Note that external hard drives
        // connected through USB are reported as fixed disks.
        return driveType switch
        {
            DriveType.Fixed => null,
            DriveType.Removable => StorageOptimizationErrorCode.RemovableVolumeNotSupported,
            DriveType.Network => StorageOptimizationErrorCode.NetworkVolumeNotSupported,
            _ => StorageOptimizationErrorCode.VolumeNotSupported,
        };
    }
}
