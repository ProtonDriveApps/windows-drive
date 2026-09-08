namespace Proton.Drive.Shared.Reporting;

public readonly record struct ErrorTag(string Key, string Value)
{
    private const string DriveSdkMetricEventKey = "DriveSdkMetricEvent";
    private const string DriveSdkVolumeTypeKey = "DriveSdkVolumeType";

    public static ErrorTag SdkUploadIntegrityError => new(DriveSdkMetricEventKey, "UploadIntegrityError");
    public static ErrorTag SdkDownloadIntegrityError => new(DriveSdkMetricEventKey, "DownloadIntegrityError");
    public static ErrorTag SdkDecryptionError => new(DriveSdkMetricEventKey, "DecryptionError");
    public static ErrorTag SdkUploadError => new(DriveSdkMetricEventKey, "UploadError");
    public static ErrorTag SdkDownloadError => new(DriveSdkMetricEventKey, "DownloadError");
    public static ErrorTag SdkVolumeType(string volumeType) => new(DriveSdkVolumeTypeKey, volumeType);
}
