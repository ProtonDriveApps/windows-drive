using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Authentication.Contracts;

internal sealed record AuthResponse : ApiResponse
{
    [JsonPropertyName("UID")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("UserID")]
    public string UserId { get; init; } = string.Empty;

    public string ServerProof { get; init; } = string.Empty;

    [JsonPropertyName("EventID")]
    public string? EventId { get; init; }

    public string? TokenType { get; init; }
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public IImmutableList<string> Scopes { get; init; } = ImmutableList<string>.Empty;
    public PasswordMode PasswordMode { get; init; }

    [JsonPropertyName("2FA")]
    public MultiFactorAuthenticationParameters? MultipleFactor { get; init; }
}
