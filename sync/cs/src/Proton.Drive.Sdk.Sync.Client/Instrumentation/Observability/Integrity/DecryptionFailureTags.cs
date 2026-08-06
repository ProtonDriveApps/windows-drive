using System.Diagnostics.CodeAnalysis;
using Proton.Drive.Sdk.Sync.Client.Sdk.Metrics;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Integrity;

internal sealed record DecryptionFailureTags(string VolumeType, string Field, string FromBefore2024)
{
    public static bool TryParse(
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        [NotNullWhen(true)] out DecryptionFailureTags? value,
        [MaybeNullWhen(false)] out string nodeUid)
    {
        if (tags.Length == 4 &&
            tags[0].Key == SdkMetrics.VolumeTypeKeyName && tags[0].Value is string volumeType &&
            tags[1].Key == IntegrityMetrics.FieldKeyName && tags[1].Value is string field &&
            tags[2].Key == IntegrityMetrics.FromBefore2024KeyName && tags[2].Value is string fromBefore2024 &&
            tags[3].Key == IntegrityMetrics.NodeUidKeyName && tags[3].Value is string parsedNodeUid)
        {
            value = new DecryptionFailureTags(volumeType, field, fromBefore2024);
            nodeUid = parsedNodeUid;
            return true;
        }

        value = null;
        nodeUid = null;
        return false;
    }
}
