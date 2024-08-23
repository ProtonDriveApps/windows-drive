using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Authentication.Contracts;

public sealed record AuthResponse : ApiResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public int ExpiresIn { get; init; }
    public string? TokenType { get; init; }
    public IImmutableList<string> Scopes { get; init; } = ImmutableList<string>.Empty;
    [JsonPropertyName("UID")]
    public string Uid { get; init; } = string.Empty;
    [JsonPropertyName("UserID")]
    public string UserId { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    [JsonPropertyName("EventID")]
    public string? EventId { get; init; }
    public string? ServerProof { get; init; }
    public PasswordMode PasswordMode { get; init; }
    [JsonPropertyName("2FA")]
    public TwoFactor? TwoFactor { get; init; }
}
