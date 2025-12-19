using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ProtonDrive.App.EarlyAccess;
using ProtonDrive.App.Localization;
using ProtonDrive.App.Windows.SystemIntegration;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.App.Windows.Views.Main.Settings;

internal class SettingsViewModel : PageViewModel, IEarlyAccessStateAware
{
    private readonly IOperatingSystemIntegrationService _operatingSystemIntegrationService;
    private readonly IEarlyAccessService _earlyAccessService;
    private readonly ILanguageService _languageService;
    private readonly SingleAction _updateEarlyAccess;

    private bool _appIsOpeningOnStartup;
    private bool _languageHasChanged;
    private bool _earlyAccessEnabled;
    private Language _selectedLanguage;

    public SettingsViewModel(
        IApp app,
        IOperatingSystemIntegrationService operatingSystemIntegrationService,
        AccountRootSyncFolderViewModel accountRootSyncFolder,
        IEarlyAccessService earlyAccessService,
        ILanguageService languageService)
    {
        AccountRootSyncFolder = accountRootSyncFolder;
        _operatingSystemIntegrationService = operatingSystemIntegrationService;
        _earlyAccessService = earlyAccessService;
        _languageService = languageService;
        _appIsOpeningOnStartup = _operatingSystemIntegrationService.GetRunApplicationOnStartup();

        SupportedLanguages = languageService.GetSupportedLanguages().ToList();
        _selectedLanguage = languageService.CurrentLanguage;

        RestartAppCommand = new AsyncRelayCommand(app.RestartAsync);
        _updateEarlyAccess = new SingleAction(UpdateEarlyAccessAsync);
    }

    public IReadOnlyList<Language> SupportedLanguages { get; }

    public Language SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value))
            {
                OnSelectedLanguageChanged(value);
            }
        }
    }

    public bool LanguageHasChanged
    {
        get => _languageHasChanged;
        private set => SetProperty(ref _languageHasChanged, value);
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

    public bool EarlyAccessEnabled
    {
        get => _earlyAccessEnabled;
        set
        {
            if (SetProperty(ref _earlyAccessEnabled, value))
            {
                _updateEarlyAccess.Cancel();
                _updateEarlyAccess.RunAsync();
            }
        }
    }

    public ICommand RestartAppCommand { get; }

    public AccountRootSyncFolderViewModel AccountRootSyncFolder { get; }

    void IEarlyAccessStateAware.OnEarlyAccessStateChanged(EarlyAccessStatus status)
    {
        EarlyAccessEnabled = status is EarlyAccessStatus.Enabled;
    }

    internal override void OnActivated()
    {
        AccountRootSyncFolder.ClearValidationResult();
    }

    private void OnSelectedLanguageChanged(Language value)
    {
        _languageService.CurrentLanguage = value;
        LanguageHasChanged = _languageService.HasLanguageChanged;
    }

    private async Task UpdateEarlyAccessAsync(CancellationToken cancellationToken)
    {
        const int throttlingBeforeExecuting = 1000;

        await Task.Delay(TimeSpan.FromMilliseconds(throttlingBeforeExecuting), cancellationToken).ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        _earlyAccessService.SetEarlyAccessStatus(EarlyAccessEnabled ? EarlyAccessStatus.Enabled : EarlyAccessStatus.Disabled);
    }
}
