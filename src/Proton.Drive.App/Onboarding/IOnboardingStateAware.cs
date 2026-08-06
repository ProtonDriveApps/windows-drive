namespace Proton.Drive.App.Onboarding;

public interface IOnboardingStateAware
{
    void OnboardingStateChanged(OnboardingState value);
}
