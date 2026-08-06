namespace Proton.Drive.App.Onboarding;

public interface IOnboardingService
{
    void CompleteStep(OnboardingStep step);
    void CompleteSharedWithMeOnboarding();
    void CompletePhotosOnboarding();
    void CompleteStorageOptimizationOnboardingStep(StorageOptimizationOnboardingStep step);
}
