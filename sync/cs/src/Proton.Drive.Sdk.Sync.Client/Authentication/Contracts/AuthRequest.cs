using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Authentication.Contracts;

internal sealed class AuthRequest
{
    public string? ClientEphemeral { get; set; }
    public string? ClientProof { get; set; }

    [JsonPropertyName("SRPSession")]
    public string? SrpSession { get; set; }

    public string? Username { get; set; }
}
