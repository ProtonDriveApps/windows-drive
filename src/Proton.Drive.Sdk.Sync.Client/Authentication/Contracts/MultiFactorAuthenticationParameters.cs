using System.Text.Json.Serialization;
using Proton.Drive.Sdk.Sync.Client.Authentication.Contracts.Fido2;

namespace Proton.Drive.Sdk.Sync.Client.Authentication.Contracts;

internal sealed class MultiFactorAuthenticationParameters
{
    [JsonPropertyName("Enabled")]
    public MultiFactorAuthenticationMethods Methods { get; init; }

    [JsonPropertyName("FIDO2")]
    public Fido2Challenge? Fido2Challenge { get; init; }
}
