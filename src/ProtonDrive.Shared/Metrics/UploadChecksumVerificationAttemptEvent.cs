namespace ProtonDrive.Shared.Metrics;

public sealed class UploadChecksumVerificationAttemptEvent : MetricEvent
{
    public bool Sha1Provided { get; init; }
}
