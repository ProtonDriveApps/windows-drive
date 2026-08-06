using System.Diagnostics.Metrics;
using Proton.Drive.Sdk.Telemetry;

namespace Proton.Drive.Sdk.Sync.Client.Sdk.Metrics;

internal sealed class DownloadMetrics
{
    public const string MeterName = "Proton.Drive.SDK.GenericDownload";
    public const string AttemptsMetricName = "proton.drive.sdk.generic.download.attempts";
    public const string FailuresMetricName = "proton.drive.sdk.generic.download.failures";
    public const string FailuresFileSizeMetricName = "proton.drive.sdk.generic.download.failures.file_size";
    public const string FailuresTransferSizeMetricName = "proton.drive.sdk.generic.download.failures.transfer_size";

    private static readonly Dictionary<DownloadError, string> DownloadErrorMapping = new()
    {
        { DownloadError.ServerError, "server_error" },
        { DownloadError.NetworkError, SdkMetrics.NetworkErrorType },
        { DownloadError.DecryptionError, "decryption_error" },
        { DownloadError.IntegrityError, "integrity_error" },
        { DownloadError.RateLimited, "rate_limited" },
        { DownloadError.HttpClientSideError, "4xx" },
        { DownloadError.ValidationError, SdkMetrics.ValidationErrorType },
        { DownloadError.Unknown, "unknown" },
    };

    private readonly Counter<int> _attempts;
    private readonly Counter<int> _failures;
    private readonly Histogram<long> _failuresFileSize;
    private readonly Histogram<long> _failuresTransferSize;

    public DownloadMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _attempts = meter.CreateCounter<int>(
            name: AttemptsMetricName,
            unit: "{number}",
            description: "Count of unique download attempts");

        _failures = meter.CreateCounter<int>(
            name: FailuresMetricName,
            unit: "{number}",
            description: "Count of failed download attempts");

        _failuresFileSize = meter.CreateHistogram(
            name: FailuresFileSizeMetricName,
            unit: "{byte}",
            description: "Total file size of the failed download",
            advice: new InstrumentAdvice<long> { HistogramBucketBoundaries = [4096, 131072, 4194304, 20971520, 1073741824, 17179869184] });

        _failuresTransferSize = meter.CreateHistogram(
            name: FailuresTransferSizeMetricName,
            unit: "{byte}",
            description: "Number of bytes downloaded before failure",
            advice: new InstrumentAdvice<long> { HistogramBucketBoundaries = [4096, 131072, 4194304, 20971520, 1073741824, 17179869184] });
    }

    public void Record(DownloadEvent downloadEvent)
    {
        if (downloadEvent.Error is not DownloadError.NetworkError and not DownloadError.ValidationError)
        {
            _attempts.Add(
                1,
                new KeyValuePair<string, object?>(SdkMetrics.VolumeTypeKeyName, MapVolumeType(downloadEvent.VolumeType)),
                new KeyValuePair<string, object?>(SdkMetrics.AttemptStatusKeyName, MapStatus(downloadEvent.Error)));
        }

        if (downloadEvent.Error is null)
        {
            return;
        }

        _failures.Add(
            1,
            new KeyValuePair<string, object?>(SdkMetrics.VolumeTypeKeyName, MapVolumeType(downloadEvent.VolumeType)),
            new KeyValuePair<string, object?>(SdkMetrics.FailureTypeKeyName, MapErrorType(downloadEvent.Error.Value)));

        if (downloadEvent.ApproximateClaimedFileSize is not null)
        {
            _failuresFileSize.Record(downloadEvent.ApproximateClaimedFileSize.Value);
        }

        _failuresTransferSize.Record(downloadEvent.ApproximateDownloadedSize);
    }

    private static string MapVolumeType(VolumeType volumeType)
    {
        return VolumeTypeMapping.GetValueOrDefault(volumeType);
    }

    private static string MapStatus(DownloadError? downloadError)
    {
        return downloadError is null ? "success" : "failure";
    }

    private static string MapErrorType(DownloadError downloadError)
    {
        return DownloadErrorMapping.GetValueOrDefault(downloadError, "unknown");
    }
}
