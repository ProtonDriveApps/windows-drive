using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Authentication.Contracts;

internal sealed record AuthResponse : UnauthenticatedSessionResponse
{
    [JsonPropertyName("UserID")]
    public string UserId { get; init; } = string.Empty;

    public string ServerProof { get; init; } = string.Empty;

    public PasswordMode PasswordMode { get; init; }

    [JsonPropertyName("2FA")]
    public MultiFactorAuthenticationParameters? MultipleFactor { get; init; }
}
