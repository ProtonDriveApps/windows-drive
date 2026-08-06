namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Integrity;

internal sealed class IntegrityMetricsSnapshot
{
    public required IReadOnlyDictionary<DecryptionFailureTags, int> DecryptionFailures { get; init; }
    public required IReadOnlyDictionary<VerificationFailureTags, int> VerificationFailures { get; init; }
    public required IReadOnlyDictionary<UploadBlockVerificationFailureTags, int> UploadBlockVerificationFailures { get; init; }
    public required IReadOnlyDictionary<UploadChecksumVerificationAttemptTags, int> UploadChecksumVerificationAttempts { get; init; }
    public required IReadOnlyDictionary<DownloadChecksumVerificationAttemptTags, int> DownloadChecksumVerificationAttempts { get; init; }
}
