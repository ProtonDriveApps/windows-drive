using Proton.Drive.Sdk.Sync.Client.Contracts;

namespace Proton.Drive.Sdk.Sync.Client.Authentication;

public interface IUserClient
{
    Task<User> GetUserAsync(CancellationToken cancellationToken);

    User? GetCachedUser();

    void SetCachedUser(User user);

    void UpdateCachedUserQuota(long? usedSpaceOrNull, long? driveUsedSpaceOrNull);

    void ClearCache();
}
