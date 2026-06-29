using System.Text.Json;
using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Authentication.Contracts.Fido2;

internal sealed class Fido2Response
{
    /// <summary>
    /// The same AuthenticationOptions received as a challenge from the server
    /// </summary>
    public required JsonElement AuthenticationOptions { get; init; }

    /// <summary>
    /// ClientData (base64) returned from the client authentication library
    /// </summary>
    public required byte[] ClientData { get; init; }

    /// <summary>
    /// AuthenticatorData (base64) returned from the client authentication library
    /// </summary>
    public required byte[] AuthenticatorData { get; init; }

    /// <summary>
    /// Signature (base64) returned from the client authentication library
    /// </summary>
    public required byte[] Signature { get; init; }

    /// <summary>
    /// CredentialID used
    /// </summary>
    [JsonPropertyName("CredentialID")]
    public required IReadOnlyList<byte> CredentialId { get; init; }
}
