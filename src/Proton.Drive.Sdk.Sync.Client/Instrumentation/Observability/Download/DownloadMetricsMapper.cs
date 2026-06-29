using System.Collections.Immutable;
using Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Shared;
using Proton.Drive.Sdk.Sync.Client.Sdk.Metrics;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Download;

internal sealed class DownloadMetricsMapper(DownloadMetricsCollector metricsCollector)
{
    private readonly HashSet<string> _volumeTypesOfFailuresImpactedUsers = [];

    public void Start()
    {
        _volumeTypesOfFailuresImpactedUsers.Clear();
        metricsCollector.Start();
    }

    public void Stop()
    {
        metricsCollector.Stop();
    }

    public ImmutableList<ObservabilityMetric> GetMetrics()
    {
        var measurementsSnapshot = metricsCollector.GetMeasurementSnapshot();
        var metrics = new List<ObservabilityMetric>();

        foreach (var (tags, value) in measurementsSnapshot.Attempts)
        {
            metrics.Add(GetSuccessRateMetric(value, tags));
        }

        foreach (var (tags, value) in measurementsSnapshot.Failures)
        {
            _volumeTypesOfFailuresImpactedUsers.Add(tags.VolumeType);

            metrics.Add(GetFailuresMetric(value, tags));
        }

        foreach (var value in measurementsSnapshot.FailuresFileSize)
        {
            metrics.Add(GetFailuresFileSizeMetric(value));
        }

        foreach (var value in measurementsSnapshot.FailuresTransferSize)
        {
            metrics.Add(GetFailuresTransferSizeMetric(value));
        }

        return metrics.ToImmutableList();
    }

    public ImmutableList<ObservabilityMetric> GetFailuresImpactedUserMetrics(string userPlan)
    {
        var metrics = new List<ObservabilityMetric>(_volumeTypesOfFailuresImpactedUsers.Count);

        foreach (var volumeType in _volumeTypesOfFailuresImpactedUsers)
        {
            var tags = new FailuresImpactedUserTags(volumeType, userPlan);
            metrics.Add(GetFailuresImpactedUserMetric(1, tags));
        }

        _volumeTypesOfFailuresImpactedUsers.Clear();

        return metrics.ToImmutableList();
    }

    private static DownloadSuccessRateMetric GetSuccessRateMetric(int value, AttemptTags tags)
    {
        var labels = new Dictionary<string, string>
        {
            { SdkMetrics.VolumeTypeKeyName, tags.VolumeType },
            { SdkMetrics.AttemptStatusKeyName, tags.Status },
        };

        return new DownloadSuccessRateMetric(value, labels);
    }

    private static DownloadFailuresMetric GetFailuresMetric(int value, FailureTags tags)
    {
        var labels = new Dictionary<string, string>
        {
            { SdkMetrics.VolumeTypeKeyName, tags.VolumeType },
            { SdkMetrics.FailureTypeKeyName, tags.Type },
        };

        return new DownloadFailuresMetric(value, labels);
    }

    private static DownloadFailuresFileSizeMetric GetFailuresFileSizeMetric(long value)
    {
        return new DownloadFailuresFileSizeMetric(value);
    }

    private static DownloadFailuresTransferSizeMetric GetFailuresTransferSizeMetric(long value)
    {
        return new DownloadFailuresTransferSizeMetric(value);
    }

    private static DownloadFailuresImpactedUserMetric GetFailuresImpactedUserMetric(int value, FailuresImpactedUserTags tags)
    {
        var labels = new Dictionary<string, string>
        {
            { SdkMetrics.VolumeTypeKeyName, tags.VolumeType },
            { SdkMetrics.UserPlanKeyName, tags.UserPlan },
        };

        return new DownloadFailuresImpactedUserMetric(value, labels);
    }
}
