namespace Proton.Drive.Sdk.Sync.Agent.Account;

public interface IUserStateAware
{
    void OnUserStateChanged(UserState value);
}
