using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Contracts;

namespace ProtonDrive.Client.Authentication;

internal class UserClient : IUserClient
{
    private readonly IUserApiClient _apiClient;

    private User? _cachedUser;

    public UserClient(IUserApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<User> GetUserAsync(CancellationToken cancellationToken)
    {
        var result = await _apiClient.GetUserAsync(cancellationToken).ThrowOnFailure().ConfigureAwait(false);

        _cachedUser = result.User;

        return result.User;
    }

    public User? GetCachedUser() => _cachedUser;

    public void ClearCache()
    {
        _cachedUser = null;
    }
}
