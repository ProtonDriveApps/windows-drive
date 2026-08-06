using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Authentication.Contracts.Fido2;

internal sealed class Fido2RegisteredKey
{
    public required string AttestationFormat { get; init; }

    [JsonPropertyName("CredentialID")]
    public required IReadOnlyList<byte> CredentialId { get; init; }

    public required string Name { get; init; }
}
