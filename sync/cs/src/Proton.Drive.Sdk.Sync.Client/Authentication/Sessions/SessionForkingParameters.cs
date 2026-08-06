using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Authentication.Sessions;

public sealed record SessionForkingParameters
{
    public string? Payload { get; set; }

    [JsonPropertyName("ChildClientID")]
    public required string ChildClientId { get; set; }

    [JsonConverter(typeof(BooleanToIntegerJsonConverter))]
    public required bool Independent { get; set; }

    public string? UserCode { get; set; }
}
