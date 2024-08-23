using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Features.Contracts;

internal sealed class FeatureListParameters
{
    /// <summary>
    /// Feature codes(s) to filter by
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; set; }
}
