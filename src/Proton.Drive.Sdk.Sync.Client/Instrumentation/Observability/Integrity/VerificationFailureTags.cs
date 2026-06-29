using System.Diagnostics.CodeAnalysis;
using Proton.Drive.Sdk.Sync.Client.Sdk.Metrics;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Integrity;

internal sealed record VerificationFailureTags(string VolumeType, string Field, string AddressMatchingDefaultShare, string FromBefore2024)
{
    public static bool TryParse(ReadOnlySpan<KeyValuePair<string, object?>> tags, [NotNullWhen(true)] out VerificationFailureTags? value)
    {
        if (tags.Length == 4 &&
            tags[0].Key == SdkMetrics.VolumeTypeKeyName && tags[0].Value is string volumeType &&
            tags[1].Key == IntegrityMetrics.FieldKeyName && tags[1].Value is string field &&
            tags[2].Key == IntegrityMetrics.AddressMatchingDefaultShareKeyName && tags[2].Value is string addressMatchingDefaultShare &&
            tags[3].Key == IntegrityMetrics.FromBefore2024KeyName && tags[3].Value is string fromBefore2024)
        {
            value = new VerificationFailureTags(volumeType, field, addressMatchingDefaultShare, fromBefore2024);
            return true;
        }

        value = null;
        return false;
    }
}
