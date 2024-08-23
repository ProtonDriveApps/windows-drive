using ProtonDrive.App.Windows.Views.Main.About;
using ProtonDrive.App.Windows.Views.Main.Account;
using ProtonDrive.App.Windows.Views.Main.Activity;
using ProtonDrive.App.Windows.Views.Main.Computers;
using ProtonDrive.App.Windows.Views.Main.Settings;
using ProtonDrive.App.Windows.Views.Main.SharedWithMe;

namespace ProtonDrive.App.Windows.Views.Main;

internal class PageViewModelFactory
{
    public PageViewModelFactory(
        SyncStateViewModel activityViewModel,
        SyncedDevicesViewModel myComputerViewModel,
        SharedWithMeViewModel sharedWithMeViewModel,
        SettingsViewModel settingsViewModel,
        AccountViewModel accountViewModel,
        AboutViewModel aboutViewModel)
    {
        ActivityViewModel = activityViewModel;
        MyComputerViewModel = myComputerViewModel;
        SharedWithMeViewModel = sharedWithMeViewModel;
        SettingsViewModel = settingsViewModel;
        AccountViewModel = accountViewModel;
        AboutViewModel = aboutViewModel;
    }

    public SyncStateViewModel ActivityViewModel { get; }
    public SyncedDevicesViewModel MyComputerViewModel { get; }
    public SharedWithMeViewModel SharedWithMeViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public AccountViewModel AccountViewModel { get; }
    public AboutViewModel AboutViewModel { get; }

    public PageViewModel? Create(ApplicationPage page)
    {
        return page switch
        {
            ApplicationPage.Activity => ActivityViewModel,
            ApplicationPage.MyComputer => MyComputerViewModel,
            ApplicationPage.SharedWithMe => SharedWithMeViewModel,
            ApplicationPage.Settings => SettingsViewModel,
            ApplicationPage.Account => AccountViewModel,
            ApplicationPage.About => AboutViewModel,
            _ => null,
        };
    }
}
