using System.Diagnostics.CodeAnalysis;
using ProtonDrive.Client.Sdk.Metrics;

namespace ProtonDrive.Client.Instrumentation.Observability.Integrity;

internal sealed record DownloadChecksumVerificationAttemptTags(string Result, string FileSize)
{
    public static bool TryParse(ReadOnlySpan<KeyValuePair<string, object?>> tags, [NotNullWhen(true)] out DownloadChecksumVerificationAttemptTags? value)
    {
        if (tags.Length == 2 &&
            tags[0].Key == IntegrityMetrics.ResultKeyName && tags[0].Value is string result &&
            tags[1].Key == IntegrityMetrics.FileSizeKeyName && tags[1].Value is string fileSize)
        {
            value = new DownloadChecksumVerificationAttemptTags(result, fileSize);
            return true;
        }

        value = null;
        return false;
    }
}
