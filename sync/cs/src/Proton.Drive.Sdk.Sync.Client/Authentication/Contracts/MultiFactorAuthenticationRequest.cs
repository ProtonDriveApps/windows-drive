using System.Text.Json.Serialization;
using Proton.Drive.Sdk.Sync.Client.Authentication.Contracts.Fido2;

namespace Proton.Drive.Sdk.Sync.Client.Authentication.Contracts;

internal sealed record MultiFactorAuthenticationRequest
{
    [JsonPropertyName("TwoFactorCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Totp { get; init; }

    [JsonPropertyName("FIDO2")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Fido2Response? Fido2Response { get; init; }
}
