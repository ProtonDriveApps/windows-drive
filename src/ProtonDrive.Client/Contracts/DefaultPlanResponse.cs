using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record DefaultPlanResponse : ApiResponse
{
    private Plan? _plan;

    [JsonPropertyName("Plans")]
    public Plan Plan
    {
        get => _plan ??= new Plan();
        init => _plan = value;
    }
}
