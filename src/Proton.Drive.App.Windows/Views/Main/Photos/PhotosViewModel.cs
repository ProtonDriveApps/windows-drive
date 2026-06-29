using Proton.Drive.App.Onboarding;

namespace Proton.Drive.App.Windows.Views.Main.Photos;

internal sealed class PhotosViewModel : PageViewModel
{
    private readonly IOnboardingService _onboardingService;

    public PhotosViewModel(IOnboardingService onboardingService, PhotosImportViewModel importViewModel)
    {
        _onboardingService = onboardingService;
        ImportViewModel = importViewModel;
    }

    public PhotosImportViewModel ImportViewModel { get; }

    internal override void OnActivated()
    {
        _onboardingService.CompletePhotosOnboarding();
        base.OnActivated();
    }
}
