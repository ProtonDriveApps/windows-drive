using Microsoft.Extensions.Logging;
using ProtonDrive.Client.Contracts;

namespace ProtonDrive.Client.Authentication;

internal class UserClient : IUserClient
{
    private readonly IUserApiClient _apiClient;
    private readonly ILogger<UserClient> _logger;
    private readonly Lock _cacheLock = new();

    private User? _cachedUser;

    public UserClient(IUserApiClient apiClient, ILogger<UserClient> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<User> GetUserAsync(CancellationToken cancellationToken)
    {
        var result = await _apiClient.GetUserAsync(cancellationToken).ThrowOnFailure().ConfigureAwait(false);

        lock (_cacheLock)
        {
            _cachedUser = result.User;
        }

        return result.User;
    }

    public User? GetCachedUser()
    {
        lock (_cacheLock)
        {
            return _cachedUser;
        }
    }

    public void SetCachedUser(User user)
    {
        lock (_cacheLock)
        {
            _cachedUser = user;
        }

        _logger.LogInformation("Cached user updated");
    }

    public void UpdateCachedUserQuota(long? usedSpaceOrNull, long? driveUsedSpaceOrNull)
    {
        if (_cachedUser is null)
        {
            return;
        }

        lock (_cacheLock)
        {
            if (_cachedUser is null)
            {
                return;
            }

            if (usedSpaceOrNull is { } usedSpace && _cachedUser.SplitStorageUsedSpace != usedSpace)
            {
                _cachedUser = _cachedUser with { SplitStorageUsedSpace = usedSpace };
            }

            if (driveUsedSpaceOrNull is { } driveUsedSpace && _cachedUser.ProductUsedSpace.Drive != driveUsedSpace)
            {
                _cachedUser = _cachedUser with { ProductUsedSpace = _cachedUser.ProductUsedSpace with { Drive = driveUsedSpace } };
            }
        }
    }

    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _cachedUser = null;
        }

        _logger.LogInformation("Cached user invalidated");
    }
}
