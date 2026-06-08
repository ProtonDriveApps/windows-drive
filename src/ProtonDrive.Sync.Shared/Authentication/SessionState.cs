using ProtonDrive.Client;

namespace ProtonDrive.App.Authentication;

public sealed class SessionState
{
    public static SessionState None { get; } = new();

    public SessionStatus Status { get; init; }
    public SigningInStatus SigningInStatus { get; init; }
    public MultiFactorAuthenticationMethods MultiFactorAuthenticationMethods { get; init; }
    public bool IsFido2Available { get; init; }
    public bool IsFirstSessionStart { get; init; }
    public IReadOnlyCollection<string> Scopes { get; init; } = [];
    public ApiResponse Response { get; init; } = ApiResponse.Success;
    public string? UserId { get; init; }
    public string? Username { get; init; }
    public string? UserEmailAddress { get; init; }
}
