using ProtonDrive.App.Windows.SystemIntegration;

namespace ProtonDrive.App.Windows.Views.Main.Settings;

internal class SettingsViewModel : PageViewModel
{
    private readonly IOperatingSystemIntegrationService _operatingSystemIntegrationService;
    private bool _appIsOpeningOnStartup;

    public SettingsViewModel(
        IOperatingSystemIntegrationService operatingSystemIntegrationService,
        AccountRootSyncFolderViewModel accountRotSyncFolder)
    {
        AccountRootSyncFolder = accountRotSyncFolder;
        _operatingSystemIntegrationService = operatingSystemIntegrationService;
        _appIsOpeningOnStartup = _operatingSystemIntegrationService.GetRunApplicationOnStartup();
    }

    internal override void OnActivated()
    {
        AccountRootSyncFolder.ClearValidationResult();
    }

    public bool AppIsOpeningOnStartup
    {
        get => _appIsOpeningOnStartup;

        set
        {
            if (SetProperty(ref _appIsOpeningOnStartup, value))
            {
                _operatingSystemIntegrationService.SetRunApplicationOnStartup(value);
            }
        }
    }

    public AccountRootSyncFolderViewModel AccountRootSyncFolder { get; }
}
