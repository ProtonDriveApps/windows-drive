using ProtonDrive.Shared.Configuration;

namespace ProtonDrive.App.Windows.Configuration.Hyperlinks;

internal sealed class ExternalHyperlinks : IExternalHyperlinks
{
    private readonly UrlConfig _urlConfig;
    private readonly IUrlOpener _urlOpener;

    public ExternalHyperlinks(UrlConfig urlConfig, IUrlOpener urlOpener)
    {
        _urlConfig = urlConfig;
        _urlOpener = urlOpener;
    }

    public IExternalHyperlink WebClient => GetHyperlink(_urlConfig.WebClient);

    public IExternalHyperlink AppDownloadPage => GetHyperlink(_urlConfig.AppDownloadPage);

    public IExternalHyperlink PrivacyPolicy => GetHyperlink(_urlConfig.PrivacyPolicy);

    public IExternalHyperlink TermsAndConditions => GetHyperlink(_urlConfig.TermsAndConditions);

    public IExternalHyperlink SignUp => GetHyperlink(_urlConfig.SignUp);

    public IExternalHyperlink ManageAccount => GetHyperlink(_urlConfig.ManageAccount);

    public IExternalHyperlink ResetPassword => GetHyperlink(_urlConfig.ResetPassword);

    public IExternalHyperlink Dashboard => GetHyperlink(_urlConfig.Dashboard);

    public IExternalHyperlink UpgradePlanFromOnboarding => GetHyperlink(_urlConfig.UpgradePlanFromOnboarding);

    public IExternalHyperlink UpgradePlanFromSidebar => GetHyperlink(_urlConfig.UpgradePlanFromSidebar);

    public IExternalHyperlink ChangePassword => GetHyperlink(_urlConfig.ChangePassword);

    public IExternalHyperlink ManageSessions => GetHyperlink(_urlConfig.ManageSessions);

    public IExternalHyperlink HowToImportPhotosFromGoogle => GetHyperlink(_urlConfig.HowToImportPhotosFromGoogle);

    public IExternalHyperlink HowPhotoImportWorks => GetHyperlink(_urlConfig.HowPhotoImportWorks);

    public IExternalHyperlink HowToUseSecurityKey => GetHyperlink(_urlConfig.HowToUseSecurityKey);

    public IExternalHyperlink ManageAlbums => GetHyperlink(_urlConfig.ManageAlbums);

    private ExternalHyperlink GetHyperlink(string url) => new(_urlOpener, url);
}
