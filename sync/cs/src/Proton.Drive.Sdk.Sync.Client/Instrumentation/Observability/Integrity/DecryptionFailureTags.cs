using System.Diagnostics.CodeAnalysis;
using Proton.Drive.Sdk.Sync.Client.Sdk.Metrics;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Integrity;

internal sealed record DecryptionFailureTags(string Field, string Recency, string CausedBy)
{
    public static bool TryParse(
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        [NotNullWhen(true)] out DecryptionFailureTags? value,
        [MaybeNullWhen(false)] out string nodeUid)
    {
        if (tags.Length == 4 &&
            tags[0].Key == IntegrityMetrics.FieldKeyName && tags[0].Value is string field &&
            tags[1].Key == IntegrityMetrics.RecencyKeyName && tags[1].Value is string recency &&
            tags[2].Key == IntegrityMetrics.CausedByKeyName && tags[2].Value is string causedBy &&
            tags[3].Key == IntegrityMetrics.NodeUidKeyName && tags[3].Value is string parsedNodeUid)
        {
            value = new DecryptionFailureTags(field, recency, causedBy);
            nodeUid = parsedNodeUid;
            return true;
        }

        value = null;
        nodeUid = null;
        return false;
    }
}
