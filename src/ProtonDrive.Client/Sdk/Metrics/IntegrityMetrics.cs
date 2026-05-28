using System.Diagnostics.Metrics;
using Proton.Drive.Sdk.Telemetry;
using ProtonDrive.Shared.Metrics;

namespace ProtonDrive.Client.Sdk.Metrics;

internal sealed class IntegrityMetrics
{
    public const string MeterName = "Proton.Drive.SDK.GenericIntegrity";
    public const string DecryptionErrorsMetricName = "proton.drive.sdk.generic.integrity.decryption_errors";
    public const string VerificationErrorsMetricName = "proton.drive.sdk.generic.integrity.verification_errors";
    public const string UploadBlockVerificationErrorsMetricName = "proton.drive.sdk.generic.integrity.upload_block_verification_errors";
    public const string UploadChecksumVerificationAttemptsMetricName = "proton.drive.sdk.generic.integrity.upload_checksum_verification_attempts";
    public const string DownloadChecksumVerificationAttemptsMetricName = "proton.drive.sdk.generic.integrity.download_checksum_verification_attempts";

    public const string FieldKeyName = "field";
    public const string FromBefore2024KeyName = "fromBefore2024";
    public const string AddressMatchingDefaultShareKeyName = "addressMatchingDefaultShare";
    public const string RetryHelpedKeyName = "retryHelped";
    public const string Sha1ProvidedKeyName = "sha1Provided";
    public const string ResultKeyName = "result";
    public const string FileSizeKeyName = "fileSize";
    public const string ChecksumVerifiedKeyName = "checksumVerified";
    public const string NodeUidKeyName = "uid";

    private const long KiB = 1024L;
    private const long MiB = 1024L * 1024;
    private const long GiB = 1024L * 1024 * 1024;

    private static readonly Dictionary<EncryptedField, string> FieldMapping = new()
    {
        { EncryptedField.ShareKey, "shareKey" },
        { EncryptedField.NodeKey, "nodeKey" },
        { EncryptedField.NodeName, "nodeName" },
        { EncryptedField.NodeHashKey, "nodeHashKey" },
        { EncryptedField.NodeExtendedAttributes, "nodeExtendedAttributes" },
        { EncryptedField.NodeContentKey, "nodeContentKey" },
        { EncryptedField.Content, "content" },
    };

    private static readonly Dictionary<ChecksumVerificationResult, string> DownloadChecksumVerificationResultMapping = new()
    {
        { ChecksumVerificationResult.Success, "success" },
        { ChecksumVerificationResult.Failure, "failure" },
        { ChecksumVerificationResult.Skipped, "skipped" },
    };

    private readonly Counter<int> _decryptionErrors;
    private readonly Counter<int> _verificationErrors;
    private readonly Counter<int> _uploadBlockVerificationErrors;
    private readonly Counter<int> _uploadChecksumVerificationAttempts;
    private readonly Counter<int> _downloadChecksumVerificationAttempts;

    public IntegrityMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _decryptionErrors = meter.CreateCounter<int>(
            name: DecryptionErrorsMetricName,
            unit: "{number}",
            description: "Count of decryption errors");

        _verificationErrors = meter.CreateCounter<int>(
            name: VerificationErrorsMetricName,
            unit: "{number}",
            description: "Count of verification errors");

        _uploadBlockVerificationErrors = meter.CreateCounter<int>(
            name: UploadBlockVerificationErrorsMetricName,
            unit: "{number}",
            description: "Count of file upload block verification errors");

        _uploadChecksumVerificationAttempts = meter.CreateCounter<int>(
            name: UploadChecksumVerificationAttemptsMetricName,
            unit: "{number}",
            description: "Count of file upload checksum verification attempts");

        _downloadChecksumVerificationAttempts = meter.CreateCounter<int>(
            name: DownloadChecksumVerificationAttemptsMetricName,
            unit: "{number}",
            description: "Count of file download checksum verification attempts");
    }

    private enum BooleanMapping
    {
        YesNo,
        TrueFalse,
    }

    public void Record(DecryptionErrorEvent metricEvent)
    {
        _decryptionErrors.Add(
            1,
            GetTag(SdkMetrics.VolumeTypeKeyName, MapVolumeType(metricEvent.VolumeType)),
            GetTag(FieldKeyName, MapField(metricEvent.Field)),
            GetTag(FromBefore2024KeyName, MapBoolean(metricEvent.FromBefore2024, BooleanMapping.YesNo)),
            GetTag(NodeUidKeyName, metricEvent.Uid.ToString()));
    }

    public void Record(VerificationErrorEvent metricEvent)
    {
        _verificationErrors.Add(
            1,
            GetTag(SdkMetrics.VolumeTypeKeyName, MapVolumeType(metricEvent.VolumeType)),
            GetTag(FieldKeyName, MapField(metricEvent.Field)),
            GetTag(AddressMatchingDefaultShareKeyName, MapBoolean(metricEvent.AddressMatchingDefaultShare, BooleanMapping.YesNo)),
            GetTag(FromBefore2024KeyName, MapBoolean(metricEvent.FromBefore2024, BooleanMapping.YesNo)));
    }

    public void Record(BlockVerificationErrorEvent metricEvent)
    {
        _uploadBlockVerificationErrors.Add(
            1,
            GetTag(RetryHelpedKeyName, MapBoolean(metricEvent.RetryHelped, BooleanMapping.YesNo)));
    }

    public void Record(UploadChecksumVerificationAttemptEvent metricEvent)
    {
        _uploadChecksumVerificationAttempts.Add(
            1,
            GetTag(Sha1ProvidedKeyName, MapBoolean(metricEvent.Sha1Provided, BooleanMapping.TrueFalse)));
    }

    public void Record(DownloadChecksumVerificationAttemptEvent metricEvent)
    {
        _downloadChecksumVerificationAttempts.Add(
            1,
            GetTag(ResultKeyName, MapChecksumVerificationResult(metricEvent.Result)),
            GetTag(FileSizeKeyName, MapChecksumVerificationFileSize(metricEvent.FileSize)),
            GetTag(ChecksumVerifiedKeyName, MapBoolean(metricEvent.ChecksumVerified, BooleanMapping.TrueFalse)));
    }

    private static string MapVolumeType(VolumeType volumeType)
    {
        return VolumeTypeMapping.GetValueOrDefault(volumeType);
    }

    private static string MapField(EncryptedField field)
    {
        return FieldMapping.GetValueOrDefault(field, "unknown");
    }

    private static string MapBoolean(bool? value, BooleanMapping mapping)
    {
        return mapping switch
        {
            BooleanMapping.YesNo => value switch
            {
                true => "yes",
                false => "no",
                null => "unknown",
            },
            BooleanMapping.TrueFalse => value switch
            {
                true => "true",
                false => "false",
                null => "unknown",
            },
            _ => "unknown",
        };
    }

    private static string MapChecksumVerificationResult(ChecksumVerificationResult value)
    {
        return DownloadChecksumVerificationResultMapping.GetValueOrDefault(value, "unknown");
    }

    private static string MapChecksumVerificationFileSize(long value)
    {
        return value switch
        {
            <= KiB => "2**10",      // Tiny file, under 1 KiB
            <= MiB => "2**20",      // Small file, under 1 MiB
            <= 4 * MiB => "2**22",  // 1 block (4 MiB)
            <= 32 * MiB => "2**25", // Medium file, under 32 MiB
            <= GiB => "2**30",      // Large file, under 1 GiB
            _ => "xxxxl",           // Over 1 GiB
        };
    }

    private static KeyValuePair<string, object?> GetTag(string key, string value)
    {
        return new KeyValuePair<string, object?>(key, value);
    }
}
