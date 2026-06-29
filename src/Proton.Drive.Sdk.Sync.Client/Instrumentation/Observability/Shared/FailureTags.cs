using System.Diagnostics.CodeAnalysis;
using Proton.Drive.Sdk.Sync.Client.Sdk.Metrics;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Shared;

internal sealed record FailureTags(string VolumeType, string Type)
{
    public static bool TryParse(ReadOnlySpan<KeyValuePair<string, object?>> tags, [NotNullWhen(true)] out FailureTags? value)
    {
        if (tags.Length == 2 &&
            tags[0].Key == SdkMetrics.VolumeTypeKeyName && tags[0].Value is string volumeType &&
            tags[1].Key == SdkMetrics.FailureTypeKeyName && tags[1].Value is string type)
        {
            value = new FailureTags(volumeType, type);
            return true;
        }

        value = null;
        return false;
    }
}
