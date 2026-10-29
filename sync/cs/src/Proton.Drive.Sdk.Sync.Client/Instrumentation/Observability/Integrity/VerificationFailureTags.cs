using System.Diagnostics.CodeAnalysis;
using Proton.Drive.Sdk.Sync.Client.Sdk.Metrics;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Integrity;

internal sealed record VerificationFailureTags(string Field, string AddressMatchingDefaultShare, string Recency, string CausedBy)
{
    public static bool TryParse(ReadOnlySpan<KeyValuePair<string, object?>> tags, [NotNullWhen(true)] out VerificationFailureTags? value)
    {
        if (tags.Length == 4 &&
            tags[0].Key == IntegrityMetrics.FieldKeyName && tags[0].Value is string field &&
            tags[1].Key == IntegrityMetrics.AddressMatchingDefaultShareKeyName && tags[1].Value is string addressMatchingDefaultShare &&
            tags[2].Key == IntegrityMetrics.RecencyKeyName && tags[2].Value is string recency &&
            tags[3].Key == IntegrityMetrics.CausedByKeyName && tags[3].Value is string causedBy)
        {
            value = new VerificationFailureTags(field, addressMatchingDefaultShare, recency, causedBy);
            return true;
        }

        value = null;
        return false;
    }
}
