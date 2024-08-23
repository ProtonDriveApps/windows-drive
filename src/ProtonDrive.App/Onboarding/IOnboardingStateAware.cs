namespace ProtonDrive.App.Onboarding;

internal interface IOnboardingStateAware
{
    void OnboardingStateChanged(OnboardingState value);
}
