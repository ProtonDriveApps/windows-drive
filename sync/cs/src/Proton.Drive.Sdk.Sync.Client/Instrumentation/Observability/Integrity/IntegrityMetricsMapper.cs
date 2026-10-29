using System.Collections.Immutable;
using Proton.Drive.Sdk.Sync.Client.Sdk.Metrics;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Integrity;

internal sealed class IntegrityMetricsMapper(IntegrityMetricsCollector metricsCollector)
{
    private readonly HashSet<string> _causesOfFailuresImpactingUsers = [];

    public void Start()
    {
        _causesOfFailuresImpactingUsers.Clear();
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
            if (IsKnownToBeFrom2024OrLater(tags.Recency))
            {
                _causesOfFailuresImpactingUsers.Add(tags.CausedBy);
            }

            metrics.Add(GetDecryptionFailuresMetric(value, tags));
        }

        foreach (var (tags, value) in measurementsSnapshot.VerificationFailures)
        {
            if (IsKnownToBeFrom2024OrLater(tags.Recency) && tags is { AddressMatchingDefaultShare: "yes" })
            {
                _causesOfFailuresImpactingUsers.Add(tags.CausedBy);
            }

            metrics.Add(GetVerificationFailuresMetric(value, tags));
        }

        foreach (var (tags, value) in measurementsSnapshot.UploadBlockVerificationFailures)
        {
            if (tags is { RetryHelped: "no" })
            {
                _causesOfFailuresImpactingUsers.Add(IntegrityMetrics.UnknownLabelValue);
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
        var metrics = new List<ObservabilityMetric>(_causesOfFailuresImpactingUsers.Count);

        foreach (var causedBy in _causesOfFailuresImpactingUsers)
        {
            var tags = new IntegrityFailuresImpactedUserTags(causedBy, userPlan);
            metrics.Add(GetFailuresImpactedUserMetric(1, tags));
        }

        _causesOfFailuresImpactingUsers.Clear();

        return metrics.ToImmutableList();
    }

    private static DecryptionFailuresMetric GetDecryptionFailuresMetric(int value, DecryptionFailureTags tags)
    {
        var labels = new Dictionary<string, string>
        {
            { IntegrityMetrics.FieldKeyName, tags.Field },
            { IntegrityMetrics.RecencyKeyName, tags.Recency },
            { IntegrityMetrics.CausedByKeyName, tags.CausedBy },
        };

        return new DecryptionFailuresMetric(value, labels);
    }

    private static VerificationFailuresMetric GetVerificationFailuresMetric(int value, VerificationFailureTags tags)
    {
        var labels = new Dictionary<string, string>
        {
            { IntegrityMetrics.FieldKeyName, tags.Field },
            { IntegrityMetrics.AddressMatchingDefaultShareKeyName, tags.AddressMatchingDefaultShare },
            { IntegrityMetrics.RecencyKeyName, tags.Recency },
            { IntegrityMetrics.CausedByKeyName, tags.CausedBy },
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

    private static IntegrityFailuresImpactedUserMetric GetFailuresImpactedUserMetric(int value, IntegrityFailuresImpactedUserTags tags)
    {
        var labels = new Dictionary<string, string>
        {
            // The metric schema still requires the volume type, but integrity events no longer report it
            { SdkMetrics.VolumeTypeKeyName, IntegrityMetrics.UnknownLabelValue },
            { SdkMetrics.UserPlanKeyName, tags.UserPlan },
            { IntegrityMetrics.CausedByKeyName, tags.CausedBy },
        };

        return new IntegrityFailuresImpactedUserMetric(value, labels);
    }

    private static bool IsKnownToBeFrom2024OrLater(string recency)
    {
        return recency is not (IntegrityMetrics.Before2024RecencyLabelValue or IntegrityMetrics.UnknownLabelValue);
    }
}
