namespace ProtonDrive.Shared.Metrics;

public sealed class DownloadChecksumVerificationAttemptEvent : MetricEvent
{
    public required ChecksumVerificationResult Result { get; init; }
    public required long FileSize { get; init; }
    public required bool ChecksumVerified { get; init; }
}
