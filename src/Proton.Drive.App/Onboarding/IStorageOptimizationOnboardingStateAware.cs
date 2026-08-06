namespace Proton.Drive.App.Onboarding;

public interface IStorageOptimizationOnboardingStateAware
{
    void StorageOptimizationOnboardingStateChanged(StorageOptimizationOnboardingStep value);
}
