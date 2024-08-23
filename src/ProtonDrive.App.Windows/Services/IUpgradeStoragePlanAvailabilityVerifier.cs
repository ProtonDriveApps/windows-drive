namespace ProtonDrive.App.Windows.Services;

internal interface IUpgradeStoragePlanAvailabilityVerifier
{
    bool UpgradedPlanIsAvailable(UpgradeStoragePlanMode mode, string? planCode);
}
