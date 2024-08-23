using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Authentication;

internal sealed record RefreshSessionParameters
{
    public RefreshSessionParameters(string refreshToken)
    {
        RefreshToken = refreshToken;
    }

    public string RefreshToken { get; }

    public string ResponseType => "token";
    public string GrantType => "refresh_token";

    [JsonPropertyName("RedirectURI")]
    public string RedirectUri => "https://proton.me";
}
