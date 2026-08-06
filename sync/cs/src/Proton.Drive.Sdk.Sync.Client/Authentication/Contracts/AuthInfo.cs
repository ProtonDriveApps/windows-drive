using System.Text.Json.Serialization;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Authentication.Contracts;

internal record AuthInfo : ApiResponse
{
    public string Modulus { get; init; } = string.Empty;
    public string ServerEphemeral { get; init; } = string.Empty;
    public int Version { get; init; }
    public ReadOnlyMemory<byte> Salt { get; init; }

    [JsonPropertyName("SRPSession")]
    public string? SrpSession { get; init; }
}
