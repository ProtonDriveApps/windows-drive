using System.Collections.Immutable;
using Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Shared;
using Proton.Drive.Sdk.Sync.Client.Sdk.Metrics;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Upload;

internal sealed class UploadMetricsMapper(UploadMetricsCollector metricsCollector)
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
            if (tags.Type is not SdkMetrics.NetworkErrorType and not SdkMetrics.ValidationErrorType)
            {
                _volumeTypesOfFailuresImpactedUsers.Add(tags.VolumeType);
            }

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

    private static UploadSuccessRateMetric GetSuccessRateMetric(int value, AttemptTags tags)
    {
        var labels = new Dictionary<string, string>
        {
            { SdkMetrics.VolumeTypeKeyName, tags.VolumeType },
            { SdkMetrics.AttemptStatusKeyName, tags.Status },
        };

        return new UploadSuccessRateMetric(value, labels);
    }

    private static UploadFailuresMetric GetFailuresMetric(int value, FailureTags tags)
    {
        var labels = new Dictionary<string, string>
        {
            { SdkMetrics.VolumeTypeKeyName, tags.VolumeType },
            { SdkMetrics.FailureTypeKeyName, tags.Type },
        };

        return new UploadFailuresMetric(value, labels);
    }

    private static UploadFailuresFileSizeMetric GetFailuresFileSizeMetric(long value)
    {
        return new UploadFailuresFileSizeMetric(value);
    }

    private static UploadFailuresTransferSizeMetric GetFailuresTransferSizeMetric(long value)
    {
        return new UploadFailuresTransferSizeMetric(value);
    }

    private static UploadFailuresImpactedUserMetric GetFailuresImpactedUserMetric(int value, FailuresImpactedUserTags tags)
    {
        var labels = new Dictionary<string, string>
        {
            { SdkMetrics.VolumeTypeKeyName, tags.VolumeType },
            { SdkMetrics.UserPlanKeyName, tags.UserPlan },
        };

        return new UploadFailuresImpactedUserMetric(value, labels);
    }
}
