using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Authentication.Contracts;

internal sealed class AuthRequest
{
    public string? ClientEphemeral { get; set; }
    public string? ClientProof { get; set; }
    [JsonPropertyName("SRPSession")]
    public string? SrpSession { get; set; }
    public string? TwoFactorCode { get; set; }
    public string? Username { get; set; }
}
