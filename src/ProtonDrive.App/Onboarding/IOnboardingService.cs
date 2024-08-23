namespace ProtonDrive.App.Onboarding;

public interface IOnboardingService
{
    void SetSyncFolderSelectionCompleted();
    bool IsSyncFolderSelectionCompleted();
    void SetAccountRootFolderSelectionCompleted();
    bool IsAccountRootFolderSelectionCompleted();
    void SetUpgradeStorageStepCompleted();
    bool IsUpgradeStorageStepCompleted();
}
