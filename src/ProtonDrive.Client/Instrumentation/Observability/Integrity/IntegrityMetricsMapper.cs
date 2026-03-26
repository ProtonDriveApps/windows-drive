using System.Collections.Immutable;
using ProtonDrive.Client.Instrumentation.Observability.Shared;
using ProtonDrive.Client.Sdk.Metrics;

namespace ProtonDrive.Client.Instrumentation.Observability.Integrity;

internal sealed class IntegrityMetricsMapper(IntegrityMetricsCollector metricsCollector)
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

        foreach (var (tags, value) in measurementsSnapshot.DecryptionFailures)
        {
            if (tags is { FromBefore2024: "no" })
            {
                _volumeTypesOfFailuresImpactedUsers.Add(tags.VolumeType);
            }

            metrics.Add(GetDecryptionFailuresMetric(value, tags));
        }

        foreach (var (tags, value) in measurementsSnapshot.VerificationFailures)
        {
            if (tags is { FromBefore2024: "no", AddressMatchingDefaultShare: "yes" })
            {
                _volumeTypesOfFailuresImpactedUsers.Add(tags.VolumeType);
            }

            metrics.Add(GetVerificationFailuresMetric(value, tags));
        }

        foreach (var (tags, value) in measurementsSnapshot.UploadBlockVerificationFailures)
        {
            if (tags is { RetryHelped: "no" })
            {
                _volumeTypesOfFailuresImpactedUsers.Add("unknown");
            }

            metrics.Add(GetUploadBlockVerificationFailuresMetric(value, tags));
        }

        foreach (var (tags, value) in measurementsSnapshot.UploadChecksumVerificationAttempts)
        {
            metrics.Add(GetUploadChecksumVerificationAttemptsMetric(value, tags));
        }

        foreach (var (tags, value) in measurementsSnapshot.DownloadChecksumVerificationAttempts)
        {
            metrics.Add(GetDownloadChecksumVerificationAttemptsMetric(value, tags));
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

    private static DecryptionFailuresMetric GetDecryptionFailuresMetric(int value, DecryptionFailureTags tags)
    {
        var labels = new Dictionary<string, string>
        {
            { SdkMetrics.VolumeTypeKeyName, tags.VolumeType },
            { IntegrityMetrics.FieldKeyName, tags.Field },
            { IntegrityMetrics.FromBefore2024KeyName, tags.FromBefore2024 },
        };

        return new DecryptionFailuresMetric(value, labels);
    }

    private static VerificationFailuresMetric GetVerificationFailuresMetric(int value, VerificationFailureTags tags)
    {
        var labels = new Dictionary<string, string>
        {
            { SdkMetrics.VolumeTypeKeyName, tags.VolumeType },
            { IntegrityMetrics.FieldKeyName, tags.Field },
            { IntegrityMetrics.AddressMatchingDefaultShareKeyName, tags.AddressMatchingDefaultShare },
            { IntegrityMetrics.FromBefore2024KeyName, tags.FromBefore2024 },
        };

        return new VerificationFailuresMetric(value, labels);
    }

    private static UploadBlockVerificationFailuresMetric GetUploadBlockVerificationFailuresMetric(int value, UploadBlockVerificationFailureTags tags)
    {
        var labels = new Dictionary<string, string>
        {
            { IntegrityMetrics.RetryHelpedKeyName, tags.RetryHelped },
        };

        return new UploadBlockVerificationFailuresMetric(value, labels);
    }

    private static UploadChecksumVerificationAttemptsMetric GetUploadChecksumVerificationAttemptsMetric(int value, UploadChecksumVerificationAttemptTags tags)
    {
        var labels = new Dictionary<string, string>
        {
            { IntegrityMetrics.Sha1ProvidedKeyName, tags.Sha1Provided },
        };

        return new UploadChecksumVerificationAttemptsMetric(value, labels);
    }

    private static DownloadChecksumVerificationAttemptsMetric GetDownloadChecksumVerificationAttemptsMetric(int value, DownloadChecksumVerificationAttemptTags tags)
    {
        var labels = new Dictionary<string, string>
        {
            { IntegrityMetrics.ResultKeyName, tags.Result },
            { IntegrityMetrics.FileSizeKeyName, tags.FileSize },
            { IntegrityMetrics.ChecksumVerifiedKeyName, tags.ChecksumVerified },
        };

        return new DownloadChecksumVerificationAttemptsMetric(value, labels);
    }

    private static IntegrityFailuresImpactedUserMetric GetFailuresImpactedUserMetric(int value, FailuresImpactedUserTags tags)
    {
        var labels = new Dictionary<string, string>
        {
            { SdkMetrics.VolumeTypeKeyName, tags.VolumeType },
            { SdkMetrics.UserPlanKeyName, tags.UserPlan },
        };

        return new IntegrityFailuresImpactedUserMetric(value, labels);
    }
}
