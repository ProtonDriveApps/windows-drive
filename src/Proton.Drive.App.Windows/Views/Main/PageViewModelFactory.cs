using Proton.Drive.App.Windows.Views.Main.About;
using Proton.Drive.App.Windows.Views.Main.Account;
using Proton.Drive.App.Windows.Views.Main.Activity;
using Proton.Drive.App.Windows.Views.Main.MyComputer;
using Proton.Drive.App.Windows.Views.Main.Photos;
using Proton.Drive.App.Windows.Views.Main.Settings;
using Proton.Drive.App.Windows.Views.Main.SharedWithMe;

namespace Proton.Drive.App.Windows.Views.Main;

internal class PageViewModelFactory
{
    public PageViewModelFactory(
        SyncStateViewModel activityViewModel,
        MyComputerViewModel myComputerViewModel,
        PhotosViewModel photosViewModel,
        SharedWithMeViewModel sharedWithMeViewModel,
        SettingsViewModel settingsViewModel,
        AccountViewModel accountViewModel,
        AboutViewModel aboutViewModel)
    {
        ActivityViewModel = activityViewModel;
        MyComputerViewModel = myComputerViewModel;
        PhotosViewModel = photosViewModel;
        SharedWithMeViewModel = sharedWithMeViewModel;
        SettingsViewModel = settingsViewModel;
        AccountViewModel = accountViewModel;
        AboutViewModel = aboutViewModel;
    }

    public SyncStateViewModel ActivityViewModel { get; }
    public MyComputerViewModel MyComputerViewModel { get; }
    public PhotosViewModel PhotosViewModel { get; }
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
            ApplicationPage.Photos => PhotosViewModel,
            ApplicationPage.SharedWithMe => SharedWithMeViewModel,
            ApplicationPage.Settings => SettingsViewModel,
            ApplicationPage.Account => AccountViewModel,
            ApplicationPage.About => AboutViewModel,
            _ => null,
        };
    }
}
