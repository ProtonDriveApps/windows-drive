namespace ProtonDrive.App.Windows.Services;

internal sealed class UpgradeStoragePlanAvailabilityVerifier : IUpgradeStoragePlanAvailabilityVerifier
{
    private const string FreeCode = "free";
    private const string VpnPlusCode = "vpn2022";
    private const string MailPlusCode = "mail2022";
    private const string PassPlusCode = "pass2023";
    private const string DrivePlusCode = "drive2022";

    private readonly HashSet<string> _eligibleToUpgradePlanCodesFromUpgradeStorageNotification = new(StringComparer.OrdinalIgnoreCase)
    {
        FreeCode,
        DrivePlusCode,
    };

    private readonly HashSet<string> _eligibleToUpgradePlanCodesDuringOnboarding = new(StringComparer.OrdinalIgnoreCase)
    {
        FreeCode,
        VpnPlusCode,
    };

    private readonly HashSet<string> _eligibleToUpgradePlanCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        FreeCode,
        VpnPlusCode,
        MailPlusCode,
        PassPlusCode,
    };

    public bool UpgradedPlanIsAvailable(UpgradeStoragePlanMode mode, string? planCode)
    {
        if (planCode is null)
        {
            return false;
        }

        return mode switch
        {
            UpgradeStoragePlanMode.Onboarding => _eligibleToUpgradePlanCodesDuringOnboarding.Contains(planCode),
            UpgradeStoragePlanMode.Sidebar => _eligibleToUpgradePlanCodes.Contains(planCode),
            UpgradeStoragePlanMode.UpgradeStorageNotification => _eligibleToUpgradePlanCodesFromUpgradeStorageNotification.Contains(planCode),
            _ => false,
        };
    }
}
