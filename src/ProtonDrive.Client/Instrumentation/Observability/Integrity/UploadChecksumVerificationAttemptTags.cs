using System.Diagnostics.CodeAnalysis;
using ProtonDrive.Client.Sdk.Metrics;

namespace ProtonDrive.Client.Instrumentation.Observability.Integrity;

internal sealed record UploadChecksumVerificationAttemptTags(string Sha1Provided)
{
    public static bool TryParse(ReadOnlySpan<KeyValuePair<string, object?>> tags, [NotNullWhen(true)] out UploadChecksumVerificationAttemptTags? value)
    {
        if (tags.Length == 1 &&
            tags[0].Key == IntegrityMetrics.Sha1ProvidedKeyName && tags[0].Value is string sha1Provided)
        {
            value = new UploadChecksumVerificationAttemptTags(sha1Provided);
            return true;
        }

        value = null;
        return false;
    }
}
