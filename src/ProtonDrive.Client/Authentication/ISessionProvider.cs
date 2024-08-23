using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Client.Authentication;

internal interface ISessionProvider
{
    Task<(Session Session, Func<CancellationToken, Task<Session?>> GetRefreshedSessionAsync)?> GetSessionAsync(CancellationToken cancellationToken);
}
