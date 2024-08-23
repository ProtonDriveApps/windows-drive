using System.Windows;
using System.Windows.Controls;
using ProtonDrive.App.Account;

namespace ProtonDrive.App.Windows.Views.Main.Account;

internal class UpgradePlanTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ExceededQuotaTemplate { get; set; }

    public DataTemplate? ExceedingQuotaTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not UserQuotaStatus state)
        {
            return null;
        }

        return state switch
        {
            UserQuotaStatus.LimitExceeded => ExceededQuotaTemplate,
            UserQuotaStatus.WarningLevel2Exceeded => ExceededQuotaTemplate,
            UserQuotaStatus.WarningLevel1Exceeded => ExceedingQuotaTemplate,
            _ => null,
        };
    }
}
