using System.Collections.Immutable;
using ProtonDrive.Client.Authentication.Contracts;

namespace ProtonDrive.Client.Authentication;

internal sealed record Session
{
    public string Id { get; init; } = string.Empty;
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public IImmutableList<string> Scopes { get; init; } = ImmutableList<string>.Empty;
    public bool TwoFactorEnabled { get; init; }
    public PasswordMode PasswordMode { get; init; }
    public string? UserId { get; init; }
    public string? Username { get; init; }
    public string? UserEmailAddress { get; init; }
}
