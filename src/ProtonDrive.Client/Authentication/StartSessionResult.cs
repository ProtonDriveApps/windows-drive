using System;
using System.Collections.Generic;

namespace ProtonDrive.Client.Authentication;

public sealed class StartSessionResult
{
    public StartSessionResultCode Code { get; private init; }
    public IReadOnlyCollection<string> Scopes { get; private init; } = Array.Empty<string>();
    public string? UserId { get; private init; }
    public string? Username { get; private init; }
    public string? UserEmailAddress { get; private init; }
    public ApiResponse Response { get; private init; } = ApiResponse.Success;

    public bool IsSuccess => Code == StartSessionResultCode.Success;

    public static StartSessionResult Failure(StartSessionResultCode code, ApiResponse? response = default)
        => new() { Code = code, Response = response ?? ApiResponse.Success };

    internal static StartSessionResult Success(Session session)
        => new()
        {
            Code = StartSessionResultCode.Success,
            Scopes = session.Scopes,
            UserId = session.UserId,
            Username = session.Username,
            UserEmailAddress = session.UserEmailAddress,
        };
}
