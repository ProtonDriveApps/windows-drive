using System;
using System.Net;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Client.Authentication;

public interface ISessionService
{
    public event EventHandler<ApiResponse>? SessionEnded;
    Task<StartSessionResult> StartSessionAsync(CancellationToken cancellationToken);
    Task<StartSessionResult> StartSessionAsync(NetworkCredential credential, CancellationToken cancellationToken);
    Task<StartSessionResult> FinishTwoFactorAuthenticationAsync(string secondFactor, CancellationToken cancellationToken);
    Task<StartSessionResult> UnlockDataAsync(SecureString dataPassword, CancellationToken cancellationToken);
    Task EndSessionAsync();
    Task EndSessionAsync(string sessionId, ApiResponse apiResponse);
}
