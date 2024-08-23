using System.Windows.Input;

namespace ProtonDrive.App.Windows.Views.Onboarding;

internal sealed record StorageUpgradeOffer(string Name, int StorageInGb, int NumberOfUsers, bool IsRecommended, ICommand UpgradeCommand)
{
    public string StorageDescription => StorageInGb < 1000 ? $"{StorageInGb} GB" : $"{StorageInGb / 1000} TB";
    public string NumberOfUsersDescription => NumberOfUsers > 1 ? $"Up to {NumberOfUsers} users" : $"{NumberOfUsers} user";
}
