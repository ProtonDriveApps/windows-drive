using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Contracts;

namespace ProtonDrive.Client.Authentication;

public interface IUserClient
{
    Task<User> GetUserAsync(CancellationToken cancellationToken);

    User? GetCachedUser();

    void ClearCache();
}
