namespace ProtonDrive.App.Account;

public interface IAccountSwitchingAware
{
    /// <summary>
    /// Notifies that account switching has successfully completed.
    /// </summary>
    /// <remarks>
    /// Account status is <see cref="AccountStatus.SettingUp"/>, syncing is terminated.
    /// </remarks>
    void OnAccountSwitched();
}
