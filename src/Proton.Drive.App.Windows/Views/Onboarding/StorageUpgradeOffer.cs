using System.Windows.Input;

namespace Proton.Drive.App.Windows.Views.Onboarding;

internal sealed record StorageUpgradeOffer(
    string Name,
    int StorageInGb,
    int NumberOfUsers,
    bool IsRecommended,
    double Price,
    string ButtonText,
    ICommand UpgradeCommand)
{
    public string StorageDescription => StorageInGb < 1000 ? $"{StorageInGb} GB" : $"{StorageInGb / 1000} TB";
    public bool IsMajorOffer => StorageInGb > 200;
    public bool IsMultiUserOffer => NumberOfUsers > 1;
}
