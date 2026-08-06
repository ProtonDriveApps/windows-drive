using System.Text.Json.Serialization;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Authentication.Contracts;

internal sealed record ModulusResponse : ApiResponse
{
    public string Modulus { get; set; } = string.Empty;

    [JsonPropertyName("ModulusID")]
    public string ModulusId { get; set; } = string.Empty;
}
