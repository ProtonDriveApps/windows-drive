using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Authentication.Contracts;

public sealed record RefreshSessionResponse : ApiResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public int ExpiresIn { get; init; }
    public string TokenType { get; init; } = string.Empty;
    public IImmutableList<string> Scopes { get; init; } = ImmutableList<string>.Empty;

    [JsonPropertyName("UID")]
    public string Uid { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
}
