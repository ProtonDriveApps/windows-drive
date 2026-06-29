using System.Diagnostics.CodeAnalysis;
using Proton.Drive.Sdk.Sync.Client.Sdk.Metrics;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Shared;

internal sealed record AttemptTags(string VolumeType, string Status)
{
    public static bool TryParse(ReadOnlySpan<KeyValuePair<string, object?>> tags, [NotNullWhen(true)] out AttemptTags? value)
    {
        if (tags.Length == 2 &&
            tags[0].Key == SdkMetrics.VolumeTypeKeyName && tags[0].Value is string volumeType &&
            tags[1].Key == SdkMetrics.AttemptStatusKeyName && tags[1].Value is string status)
        {
            value = new AttemptTags(volumeType, status);
            return true;
        }

        value = null;
        return false;
    }
}
