namespace Proton.Drive.Sdk.Sync.Agent.Account;

internal interface IAccountSwitchingService
{
    bool IsAccountSwitchingRequired(string? userId);
    Task<bool> SwitchAccountAsync(string? userId, CancellationToken cancellationToken);
}
