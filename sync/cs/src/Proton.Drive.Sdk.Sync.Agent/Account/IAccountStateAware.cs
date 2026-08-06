namespace Proton.Drive.Sdk.Sync.Agent.Account;

public interface IAccountStateAware
{
    void OnAccountStateChanged(AccountState value);
}
