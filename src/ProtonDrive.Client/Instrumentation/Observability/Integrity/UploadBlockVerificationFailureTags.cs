using System.Diagnostics.CodeAnalysis;
using ProtonDrive.Client.Sdk.Metrics;

namespace ProtonDrive.Client.Instrumentation.Observability.Integrity;

internal sealed record UploadBlockVerificationFailureTags(string RetryHelped)
{
    public static bool TryParse(ReadOnlySpan<KeyValuePair<string, object?>> tags, [NotNullWhen(true)] out UploadBlockVerificationFailureTags? value)
    {
        if (tags.Length == 1 &&
            tags[0].Key == IntegrityMetrics.RetryHelpedKeyName && tags[0].Value is string retryHelped)
        {
            value = new UploadBlockVerificationFailureTags(retryHelped);
            return true;
        }

        value = null;
        return false;
    }
}
