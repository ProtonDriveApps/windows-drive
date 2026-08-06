namespace Proton.Drive.Shared.Reporting;

public readonly record struct ErrorTag(string Key, string Value)
{
    private const string DriveSdkMetricEventKey = "DriveSdkMetricEvent";

    public static ErrorTag SdkIntegrityUploadError => new(DriveSdkMetricEventKey, "IntegrityUploadError");
    public static ErrorTag SdkIntegrityDownloadError => new(DriveSdkMetricEventKey, "IntegrityDownloadError");
    public static ErrorTag SdkDecryptionError => new(DriveSdkMetricEventKey, "DecryptionError");
    public static ErrorTag SdkUploadError => new(DriveSdkMetricEventKey, "UploadError");
    public static ErrorTag SdkDownloadError => new(DriveSdkMetricEventKey, "DownloadError");
}
