using System.Text.Json.Serialization;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed record DefaultPlanResponse : ApiResponse
{
    private readonly Plan? _plan;

    [JsonPropertyName("Plans")]
    public Plan Plan
    {
        get => _plan ?? throw new ApiException("Plan is not set");
        init => _plan = value;
    }
}
