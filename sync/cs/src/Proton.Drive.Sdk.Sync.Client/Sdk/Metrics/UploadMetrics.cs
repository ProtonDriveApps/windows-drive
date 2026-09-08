using System.Diagnostics.Metrics;
using Proton.Drive.Sdk.Telemetry;

namespace Proton.Drive.Sdk.Sync.Client.Sdk.Metrics;

internal sealed class UploadMetrics
{
    public const string MeterName = "Proton.Drive.SDK.GenericUpload";
    public const string AttemptsMetricName = "proton.drive.sdk.generic.upload.attempts";
    public const string FailuresMetricName = "proton.drive.sdk.generic.upload.failures";
    public const string FailuresFileSizeMetricName = "proton.drive.sdk.generic.upload.failures.file_size";
    public const string FailuresTransferSizeMetricName = "proton.drive.sdk.generic.upload.failures.transfer_size";
    public const string SmallFileThroughputMetricName = "proton.drive.sdk.upload.small_file_throughput";
    public const string LargeFileThroughputMetricName = "proton.drive.sdk.upload.large_file_throughput";
    public const string LargeRouteActiveTimeShareMetricName = "proton.drive.sdk.upload.large_route_active_time_share";
    public const string SmallRouteActiveTimeShareMetricName = "proton.drive.sdk.upload.small_route_active_time_share";
    public const string UploadRouteKeyName = "uploadRoute";
    public const string BlockCountKeyName = "blockCount";
    public const string SizeClassKeyName = "sizeClass";

    private static readonly Dictionary<UploadRoute, string> UploadRouteMapping = new()
    {
        { UploadRoute.Small, "small" },
        { UploadRoute.Block, "block" },
    };

    private static readonly Dictionary<UploadBlockCount, string> UploadBlockCountMapping = new()
    {
        { UploadBlockCount.Single, "single" },
        { UploadBlockCount.Few, "few" },
        { UploadBlockCount.Many, "many" },
    };

    private static readonly Dictionary<UploadSizeClass, string> UploadSizeClassMapping = new()
    {
        { UploadSizeClass.Small, "small" },
        { UploadSizeClass.Single, "single" },
        { UploadSizeClass.Multi, "multi" },
    };

    private static readonly Dictionary<UploadError, string> UploadErrorMapping = new()
    {
        { UploadError.ServerError, "server_error" },
        { UploadError.NetworkError, SdkMetrics.NetworkErrorType },
        { UploadError.IntegrityError, "integrity_error" },
        { UploadError.RateLimited, "rate_limited" },
        { UploadError.HttpClientSideError, "4xx" },
        { UploadError.ValidationError, SdkMetrics.ValidationErrorType },
        { UploadError.Unknown, "unknown" },
    };

    private readonly Counter<int> _attempts;
    private readonly Counter<int> _failures;
    private readonly Histogram<long> _failuresFileSize;
    private readonly Histogram<long> _failuresTransferSize;
    private readonly Histogram<long> _smallFileThroughput;
    private readonly Histogram<long> _largeFileThroughput;
    private readonly Histogram<long> _largeRouteActiveTimeShare;
    private readonly Histogram<long> _smallRouteActiveTimeShare;

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

        _smallFileThroughput = meter.CreateHistogram(
            name: SmallFileThroughputMetricName,
            unit: "KiBy/s",
            description: "Per-file upload throughput for files smaller than 128 KiB",
            advice: new InstrumentAdvice<long> { HistogramBucketBoundaries = [4, 8, 16, 32, 64, 128, 256, 512, 1024] });

        _largeFileThroughput = meter.CreateHistogram(
            name: LargeFileThroughputMetricName,
            unit: "KiBy/s",
            description: "Per-file upload throughput for files at least 128 KiB",
            advice: new InstrumentAdvice<long> { HistogramBucketBoundaries = [8, 32, 128, 512, 2048, 8192] });

        _largeRouteActiveTimeShare = meter.CreateHistogram(
            name: LargeRouteActiveTimeShareMetricName,
            unit: "%",
            description: "Share of total upload time spent active on the block upload route",
            advice: new InstrumentAdvice<long> { HistogramBucketBoundaries = [25, 50, 75, 80, 85, 90, 95, 100] });

        _smallRouteActiveTimeShare = meter.CreateHistogram(
            name: SmallRouteActiveTimeShareMetricName,
            unit: "%",
            description: "Share of total upload time spent active on the small-file upload route",
            advice: new InstrumentAdvice<long> { HistogramBucketBoundaries = [10, 15, 20, 25, 50, 75, 100] });
    }

    public void Record(UploadEvent uploadEvent)
    {
        if (uploadEvent.Error is not UploadError.NetworkError and not UploadError.ValidationError)
        {
            _attempts.Add(
                1,
                new KeyValuePair<string, object?>(SdkMetrics.VolumeTypeKeyName, MapVolumeType(uploadEvent.VolumeType)),
                new KeyValuePair<string, object?>(SdkMetrics.AttemptStatusKeyName, MapStatus(uploadEvent.Error)));
        }

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

    public void Record(UploadPerformanceEvent uploadPerformanceEvent)
    {
        if (uploadPerformanceEvent.Metric is UploadPerformanceMetric.SmallFileThroughput &&
            UploadRouteMapping.TryGetValue(uploadPerformanceEvent.UploadRoute, out var uploadRoute))
        {
            _smallFileThroughput.Record(
                uploadPerformanceEvent.Value,
                new KeyValuePair<string, object?>(UploadRouteKeyName, uploadRoute));
        }
        else if (uploadPerformanceEvent.Metric is UploadPerformanceMetric.LargeFileThroughput &&
                 UploadBlockCountMapping.TryGetValue(uploadPerformanceEvent.BlockCount, out var blockCount))
        {
            _largeFileThroughput.Record(
                uploadPerformanceEvent.Value,
                new KeyValuePair<string, object?>(BlockCountKeyName, blockCount));
        }
        else if (uploadPerformanceEvent.Metric is UploadPerformanceMetric.LargeRouteActiveTimeShare &&
                 UploadSizeClassMapping.TryGetValue(uploadPerformanceEvent.SizeClass, out var sizeClass))
        {
            _largeRouteActiveTimeShare.Record(
                uploadPerformanceEvent.Value,
                new KeyValuePair<string, object?>(SizeClassKeyName, sizeClass));
        }
        else if (uploadPerformanceEvent.Metric is UploadPerformanceMetric.SmallRouteActiveTimeShare)
        {
            _smallRouteActiveTimeShare.Record(uploadPerformanceEvent.Value);
        }
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
