using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Authentication.Contracts;

public record AuthInfo : ApiResponse
{
    public string? Modulus { get; init; }
    public string? ServerEphemeral { get; init; }
    public int Version { get; init; }
    public string? Salt { get; init; }
    [JsonPropertyName("SRPSession")]
    public string? SrpSession { get; init; }
    public int TwoFactor { get; init; }
}
