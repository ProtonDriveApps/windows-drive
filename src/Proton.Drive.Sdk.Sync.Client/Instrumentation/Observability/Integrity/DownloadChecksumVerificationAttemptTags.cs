using System.Diagnostics.CodeAnalysis;
using Proton.Drive.Sdk.Sync.Client.Sdk.Metrics;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Integrity;

internal sealed record DownloadChecksumVerificationAttemptTags(string Result, string FileSize, string ChecksumVerified)
{
    public static bool TryParse(ReadOnlySpan<KeyValuePair<string, object?>> tags, [NotNullWhen(true)] out DownloadChecksumVerificationAttemptTags? value)
    {
        if (tags.Length == 3 &&
            tags[0].Key == IntegrityMetrics.ResultKeyName && tags[0].Value is string result &&
            tags[1].Key == IntegrityMetrics.FileSizeKeyName && tags[1].Value is string fileSize &&
            tags[2].Key == IntegrityMetrics.ChecksumVerifiedKeyName && tags[2].Value is string checksumVerified)
        {
            value = new DownloadChecksumVerificationAttemptTags(result, fileSize, checksumVerified);
            return true;
        }

        value = null;
        return false;
    }
}
