namespace Proton.Drive.App.Onboarding;

public interface IPhotosOnboardingStateAware
{
    void OnPhotosOnboardingStateChanged(OnboardingStatus value);
}
