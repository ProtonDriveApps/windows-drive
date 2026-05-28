using System.Diagnostics.Metrics;
using Proton.Drive.Sdk.Telemetry;

namespace ProtonDrive.Client.Sdk.Metrics;

internal sealed class UploadMetrics
{
    public const string MeterName = "Proton.Drive.SDK.GenericUpload";
    public const string AttemptsMetricName = "proton.drive.sdk.generic.upload.attempts";
    public const string FailuresMetricName = "proton.drive.sdk.generic.upload.failures";
    public const string FailuresFileSizeMetricName = "proton.drive.sdk.generic.upload.failures.file_size";
    public const string FailuresTransferSizeMetricName = "proton.drive.sdk.generic.upload.failures.transfer_size";

    private static readonly Dictionary<UploadError, string> UploadErrorMapping = new()
    {
        { UploadError.ServerError, "server_error" },
        { UploadError.NetworkError, "network_error" },
        { UploadError.IntegrityError, "integrity_error" },
        { UploadError.RateLimited, "rate_limited" },
        { UploadError.HttpClientSideError, "4xx" },
        { UploadError.Unknown, "unknown" },
    };

    private readonly Counter<int> _attempts;
    private readonly Counter<int> _failures;
    private readonly Histogram<long> _failuresFileSize;
    private readonly Histogram<long> _failuresTransferSize;

    public UploadMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _attempts = meter.CreateCounter<int>(
            name: AttemptsMetricName,
            unit: "{number}",
            description: "Count of unique upload attempts");

        _failures = meter.CreateCounter<int>(
            name: FailuresMetricName,
            unit: "{number}",
            description: "Count of failed upload attempts");

        _failuresFileSize = meter.CreateHistogram(
            name: FailuresFileSizeMetricName,
            unit: "{byte}",
            description: "Total file size of the failed upload",
            advice: new InstrumentAdvice<long> { HistogramBucketBoundaries = [4096, 131072, 4194304, 20971520, 1073741824, 17179869184] });

        _failuresTransferSize = meter.CreateHistogram(
            name: FailuresTransferSizeMetricName,
            unit: "{byte}",
            description: "Number of bytes uploaded before failure",
            advice: new InstrumentAdvice<long> { HistogramBucketBoundaries = [4096, 131072, 4194304, 20971520, 1073741824, 17179869184] });
    }

    public void Record(UploadEvent uploadEvent)
    {
        _attempts.Add(
            1,
            new KeyValuePair<string, object?>(SdkMetrics.VolumeTypeKeyName, MapVolumeType(uploadEvent.VolumeType)),
            new KeyValuePair<string, object?>(SdkMetrics.AttemptStatusKeyName, MapStatus(uploadEvent.Error)));

        if (uploadEvent.Error is null)
        {
            return;
        }

        _failures.Add(
            1,
            new KeyValuePair<string, object?>(SdkMetrics.VolumeTypeKeyName, MapVolumeType(uploadEvent.VolumeType)),
            new KeyValuePair<string, object?>(SdkMetrics.FailureTypeKeyName, MapErrorType(uploadEvent.Error.Value)));

        _failuresFileSize.Record(uploadEvent.ApproximateExpectedSize);
        _failuresTransferSize.Record(uploadEvent.ApproximateUploadedSize);
    }

    private static string MapVolumeType(VolumeType volumeType)
    {
        return VolumeTypeMapping.GetValueOrDefault(volumeType);
    }

    private static string MapStatus(UploadError? uploadError)
    {
        return uploadError is null ? "success" : "failure";
    }

    private static string MapErrorType(UploadError uploadError)
    {
        return UploadErrorMapping.GetValueOrDefault(uploadError, "unknown");
    }
}
