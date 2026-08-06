using Proton.Drive.Shared;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Authentication;

public sealed class StartSessionResult
{
    public StartSessionResultCode Code { get; private init; }
    public IReadOnlyCollection<string> Scopes { get; private init; } = [];
    public string? UserId { get; private init; }
    public string? Username { get; private init; }
    public string? UserEmailAddress { get; private init; }
    public ApiResponse Response { get; private init; } = ApiResponse.Success;
    public MultiFactorAuthenticationParameters? MultiFactor { get; private set; }
    public bool IsSuccess => Code == StartSessionResultCode.Success;

    public static StartSessionResult Failure(StartSessionResultCode code, ApiResponse? response = null)
        => new() { Code = code, Response = response ?? ApiResponse.Success };

    internal static StartSessionResult SecondFactorAuthenticationRequired(Contracts.MultiFactorAuthenticationParameters? parameters, ApiResponse? response = null)
    {
        Ensure.NotNull(parameters, nameof(parameters));

        return new StartSessionResult
        {
            Code = StartSessionResultCode.SecondFactorRequired,
            Response = response ?? ApiResponse.Success,
            MultiFactor = parameters.GetMultiFactorParameters(),
        };
    }

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
