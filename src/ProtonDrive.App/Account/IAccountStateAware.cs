namespace ProtonDrive.App.Account;

public interface IAccountStateAware
{
    void OnAccountStateChanged(AccountState value);
}
