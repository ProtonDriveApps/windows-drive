using System.Net;
using System.Security;

namespace Proton.Drive.Sdk.Sync.Shared.Authentication;

public interface IAuthenticationService
{
    Task AuthenticateAsync(NetworkCredential credential);
    Task AuthenticateWithTotpAsync(string secondFactor);
    Task AuthenticateWithFido2Async();
    Task FinishTwoPasswordAuthenticationAsync(SecureString secondPassword);
    Task CancelAuthenticationAsync();
    void RestartAuthentication();
}
