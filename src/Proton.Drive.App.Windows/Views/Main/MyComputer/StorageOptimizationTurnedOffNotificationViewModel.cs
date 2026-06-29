using Proton.Drive.App.Windows.Dialogs;

namespace Proton.Drive.App.Windows.Views.Main.MyComputer;

internal sealed class StorageOptimizationTurnedOffNotificationViewModel : ConfirmationDialogViewModelBase
{
    private static readonly string ContentText =
        Resources.Strings.Main_MyComputer_Folders_StorageOptimizationTurnedOffNotification_Message_1 + Environment.NewLine + Environment.NewLine +
        Resources.Strings.Main_MyComputer_Folders_StorageOptimizationTurnedOffNotification_Message_2;

    public StorageOptimizationTurnedOffNotificationViewModel()
        : base(Resources.Strings.Main_MyComputer_Folders_StorageOptimizationTurnedOffNotification_Title, message: ContentText)
    {
        IsCancelButtonVisible = false;
    }

    public void SetArguments(string folderName)
    {
        Message = string.Format(ContentText, folderName);
    }
}
