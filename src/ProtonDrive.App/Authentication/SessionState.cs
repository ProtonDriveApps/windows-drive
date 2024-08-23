using System.Collections.Generic;
using System.Collections.Immutable;
using ProtonDrive.Client;

namespace ProtonDrive.App.Authentication;

public sealed class SessionState
{
    public SessionStatus Status { get; init; }
    public SigningInStatus SigningInStatus { get; init; }
    public bool IsFirstSessionStart { get; init; }
    public IReadOnlyCollection<string> Scopes { get; init; } = ImmutableList<string>.Empty;
    public ApiResponse Response { get; init; } = ApiResponse.Success;
    public string? UserId { get; init; }
    public string? Username { get; init; }
    public string? UserEmailAddress { get; init; }

    public static SessionState None { get; } = new();
}
