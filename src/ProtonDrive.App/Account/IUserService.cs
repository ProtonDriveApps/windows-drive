using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Contracts;

namespace ProtonDrive.App.Account;

internal interface IUserService
{
    void Start(IReadOnlyCollection<string> sessionScopes);
    Task StopAsync();
    Task<UserState> GetUserAsync(CancellationToken cancellationToken);
    void ApplyUpdate(User? user = null, Organization? organization = null, UserSubscription? subscription = null, long? usedSpace = null);
    void Refresh();
}
