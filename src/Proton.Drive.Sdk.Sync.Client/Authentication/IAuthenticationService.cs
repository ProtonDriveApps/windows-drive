using System.Net;
using System.Security;
using Proton.Drive.Shared.Authentication;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Authentication;

public interface IAuthenticationService
{
    public event EventHandler<StartSessionResult>? SessionStarted;
    public event EventHandler<ApiResponse>? SessionEndedImplicitly;

    Task<StartSessionResult> StartSessionAsync(CancellationToken cancellationToken);
    Task<StartSessionResult> StartSessionAsync(NetworkCredential credential, CancellationToken cancellationToken);
    Task<StartSessionResult> AuthenticateWithTotpAsync(string totp, CancellationToken cancellationToken);
    Task<StartSessionResult> AuthenticateWithFido2Async(Fido2AssertionResult fido2Response, CancellationToken cancellationToken);
    Task<StartSessionResult> UnlockDataAsync(SecureString dataPassword, CancellationToken cancellationToken);

    Task EndSessionAsync();
    Task EndSessionAsync(string sessionId, ApiResponse apiResponse);
}
