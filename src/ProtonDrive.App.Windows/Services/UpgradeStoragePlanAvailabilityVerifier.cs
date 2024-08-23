using System;
using System.Collections.Generic;

namespace ProtonDrive.App.Windows.Services;

internal sealed class UpgradeStoragePlanAvailabilityVerifier : IUpgradeStoragePlanAvailabilityVerifier
{
    private const string FreeCode = "free";
    private const string VpnPlusCode = "vpn2022";
    private const string MailPlusCode = "mail2022";
    private const string PassPlusCode = "pass2023";

    private readonly IReadOnlySet<string> _eligibleToUpgradePlanCodesDuringOnboarding = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        FreeCode,
        VpnPlusCode,
    };

    private readonly IReadOnlySet<string> _eligibleToUpgradePlanCodes = new HashSet<string>
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
            _ => false,
        };
    }
}
