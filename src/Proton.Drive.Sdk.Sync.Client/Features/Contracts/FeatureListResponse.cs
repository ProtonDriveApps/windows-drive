using System.Text.Json.Serialization;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Features.Contracts;

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
