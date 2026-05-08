using Microsoft.Extensions.Logging;
using ProtonDrive.Client.Contracts;

namespace ProtonDrive.Client.Authentication;

internal class UserClient : IUserClient
{
    private readonly IUserApiClient _apiClient;
    private readonly ILogger<UserClient> _logger;

    private User? _cachedUser;

    public UserClient(IUserApiClient apiClient, ILogger<UserClient> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
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
        _logger.LogInformation("Cached user invalidated");
    }
}
