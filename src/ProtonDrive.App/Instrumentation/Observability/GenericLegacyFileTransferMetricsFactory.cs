using System.Collections.Immutable;
using ProtonDrive.Client.Instrumentation.Observability;

namespace ProtonDrive.App.Instrumentation.Observability;

internal sealed class GenericLegacyFileTransferMetricsFactory
{
    private readonly AttemptRetryMonitors _attemptRetryMonitors;

    public GenericLegacyFileTransferMetricsFactory(AttemptRetryMonitors attemptRetryMonitors)
    {
        _attemptRetryMonitors = attemptRetryMonitors;
    }

    public ImmutableList<ObservabilityMetric> GetLegacyFileUploadMetrics()
    {
        var shareTypes = Enum.GetValues(typeof(AttemptRetryShareType));

        var uploadMetrics = new List<ObservabilityMetric>(capacity: shareTypes.Length * 4);

        foreach (AttemptRetryShareType shareType in shareTypes)
        {
            var counters = _attemptRetryMonitors.UploadAttemptRetryMonitor[shareType].GetAndResetCounters();

            if (counters.FirstAttemptSuccesses > 0)
            {
                uploadMetrics.Add(GetLegacyUploadMetric(counters.FirstAttemptSuccesses, isSuccess: true, isRetry: false, shareType));
            }

            if (counters.FirstAttemptFailures > 0)
            {
                uploadMetrics.Add(GetLegacyUploadMetric(counters.FirstAttemptFailures, isSuccess: false, isRetry: false, shareType));
            }

            if (counters.RetrySuccesses > 0)
            {
                uploadMetrics.Add(GetLegacyUploadMetric(counters.RetrySuccesses, isSuccess: true, isRetry: true, shareType));
            }

            if (counters.RetryFailures > 0)
            {
                uploadMetrics.Add(GetLegacyUploadMetric(counters.RetryFailures, isSuccess: false, isRetry: true, shareType));
            }
        }

        return uploadMetrics.ToImmutableList();
    }

    public ImmutableList<ObservabilityMetric> GetLegacyFileDownloadMetrics()
    {
        var shareTypes = Enum.GetValues(typeof(AttemptRetryShareType));

        var downloadsMetrics = new List<ObservabilityMetric>(capacity: shareTypes.Length * 4);

        foreach (AttemptRetryShareType shareType in shareTypes)
        {
            var counters = _attemptRetryMonitors.DownloadAttemptRetryMonitor[shareType].GetAndResetCounters();

            if (counters.FirstAttemptSuccesses > 0)
            {
                downloadsMetrics.Add(GetLegacyDownloadMetric(counters.FirstAttemptSuccesses, isSuccess: true, isRetry: false, shareType));
            }

            if (counters.FirstAttemptFailures > 0)
            {
                downloadsMetrics.Add(GetLegacyDownloadMetric(counters.FirstAttemptFailures, isSuccess: false, isRetry: false, shareType));
            }

            if (counters.RetrySuccesses > 0)
            {
                downloadsMetrics.Add(GetLegacyDownloadMetric(counters.RetrySuccesses, isSuccess: true, isRetry: true, shareType));
            }

            if (counters.RetryFailures > 0)
            {
                downloadsMetrics.Add(GetLegacyDownloadMetric(counters.RetryFailures, isSuccess: false, isRetry: true, shareType));
            }
        }

        return downloadsMetrics.ToImmutableList();
    }

    public void Clear()
    {
        _attemptRetryMonitors.Clear();
    }

    private static LegacyUploadSuccessRateMetric GetLegacyUploadMetric(int counter, bool isSuccess, bool isRetry, AttemptRetryShareType shareType)
    {
        var shareTypeLabel = shareType switch
        {
            AttemptRetryShareType.Main => "main",
            AttemptRetryShareType.Standard => "shared",
            AttemptRetryShareType.Device => "device",
            AttemptRetryShareType.Photo => "photo",
            _ => throw new ArgumentOutOfRangeException(nameof(shareType), shareType, null),
        };

        var labels = new Dictionary<string, string>
        {
            { "status", isSuccess ? "success" : "failure" },
            { "retry", isRetry ? "true" : "false" },
            { "shareType", shareTypeLabel },
            { "initiator", "background" },
        };

        var properties = new ObservabilityMetricProperties(Value: counter, labels);
        return new LegacyUploadSuccessRateMetric(properties);
    }

    private static LegacyDownloadSuccessRateMetric GetLegacyDownloadMetric(int counter, bool isSuccess, bool isRetry, AttemptRetryShareType shareType)
    {
        var shareTypeLabel = shareType switch
        {
            AttemptRetryShareType.Main => "main",
            AttemptRetryShareType.Standard => "shared",
            AttemptRetryShareType.Device => "device",
            _ => throw new ArgumentOutOfRangeException(nameof(shareType), shareType, null),
        };

        var labels = new Dictionary<string, string>
        {
            { "status", isSuccess ? "success" : "failure" },
            { "retry", isRetry ? "true" : "false" },
            { "shareType", shareTypeLabel },
        };

        var properties = new ObservabilityMetricProperties(Value: counter, labels);
        return new LegacyDownloadSuccessRateMetric(properties);
    }
}
