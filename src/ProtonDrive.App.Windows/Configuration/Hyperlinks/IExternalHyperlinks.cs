namespace ProtonDrive.App.Windows.Configuration.Hyperlinks;

public interface IExternalHyperlinks
{
    IExternalHyperlink WebClient { get; }
    IExternalHyperlink AppDownloadPage { get; }
    IExternalHyperlink PrivacyPolicy { get; }
    IExternalHyperlink TermsAndConditions { get; }
    IExternalHyperlink SignUp { get; }
    IExternalHyperlink ManageAccount { get; }
    IExternalHyperlink ResetPassword { get; }
    IExternalHyperlink Dashboard { get; }
    IExternalHyperlink UpgradePlanFromOnboarding { get; }
    IExternalHyperlink UpgradePlanFromSidebar { get; }
    IExternalHyperlink ChangePassword { get; }
    IExternalHyperlink ManageSessions { get; }
}
