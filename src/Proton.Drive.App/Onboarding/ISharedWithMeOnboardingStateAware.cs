namespace Proton.Drive.App.Onboarding;

public interface ISharedWithMeOnboardingStateAware
{
    void SharedWithMeOnboardingStateChanged(OnboardingStatus value);
}
