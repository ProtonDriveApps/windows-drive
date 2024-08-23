using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Features.Contracts;

public sealed record FeatureListResponse : ApiResponse
{
    private IReadOnlyCollection<FeatureFlag>? _featureFlags;

    [JsonPropertyName("toggles")]
    public IReadOnlyCollection<FeatureFlag> FeatureFlags
    {
        get => _featureFlags ??= [];
        init => _featureFlags = value;
    }
}
