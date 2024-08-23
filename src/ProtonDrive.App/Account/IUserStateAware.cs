namespace ProtonDrive.App.Account;

public interface IUserStateAware
{
    void OnUserStateChanged(UserState value);
}
