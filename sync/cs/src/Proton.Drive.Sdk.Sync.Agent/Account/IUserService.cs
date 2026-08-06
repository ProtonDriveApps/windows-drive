using Proton.Drive.Sdk.Sync.Client.Contracts;

namespace Proton.Drive.Sdk.Sync.Agent.Account;

internal interface IUserService
{
    void Start(IReadOnlyCollection<string> sessionScopes);

    Task StopAsync();

    void Refresh();

    Task<UserState> GetUserAsync(CancellationToken cancellationToken);

    void ApplyUpdate(
        User? user = null,
        Organization? organization = null,
        UserSubscription? subscription = null,
        long? usedSpace = null,
        long? driveUsedSpace = null);
}
