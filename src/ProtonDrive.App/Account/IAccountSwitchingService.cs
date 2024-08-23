using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.App.Account;

internal interface IAccountSwitchingService
{
    bool IsAccountSwitchingRequired(string? userId);
    Task<bool> SwitchAccountAsync(string? userId, CancellationToken cancellationToken);
}
