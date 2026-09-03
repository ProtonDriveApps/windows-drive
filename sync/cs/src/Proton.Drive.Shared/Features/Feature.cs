using System.Text.Json.Serialization;

namespace Proton.Drive.Shared.Features;

[JsonConverter(typeof(JsonStringEnumConverter<Feature>))]
public enum Feature
{
    DriveAlbumsDisabled,
    DriveCryptoEncryptBlocksWithPgpAead,
    DriveCryptoEncryptBlocksWithPgpAeadDisabled,
    DriveDownloadVerificationDisabled,
    DrivePhotosUploadDisabled,
    DriveSmallFileUpload,
    DriveSharingDisabled,
    DriveSharingEditingDisabled,
    DriveUploadVerificationDisabled,
    DriveWindowsBulkDeletionSquashingDisabled,
    DriveWindowsDeviceEventHandlingDisabled,
    DriveWindowsFileConsistencyGuard,
    DriveWindowsFileConsistencyGuardDownloadWave1,
    DriveWindowsFileConsistencyGuardDownloadWave2,
    DriveWindowsFileConsistencyGuardSanitization,
    DriveWindowsForceMigrationToVolumeEvents,
    DriveWindowsOffers,
    DriveWindowsOffersSystemNotificationPopup,
    DriveWindowsStorageOptimizationDisabled,
    DriveWindowsTwoPassUpdateDetectionDisabled,
}
