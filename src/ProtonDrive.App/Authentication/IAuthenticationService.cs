using System.Net;
using System.Security;
using System.Threading.Tasks;

namespace ProtonDrive.App.Authentication;

public interface IAuthenticationService
{
    Task AuthenticateAsync(NetworkCredential credential);
    Task FinishTwoFactorAuthenticationAsync(string secondFactor);
    Task FinishTwoPasswordAuthenticationAsync(SecureString secondPassword);
    Task CancelAuthenticationAsync();
    void RestartAuthentication();
}
