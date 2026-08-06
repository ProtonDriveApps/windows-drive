namespace Proton.Drive.App.Windows.Views.Main.MyComputer;

internal sealed class RemoveOnDemandFolderConfirmationViewModel : RemoveFolderConfirmationViewModelBase
{
    private static readonly string ContentText =
        Resources.Strings.Main_MyComputer_Folders_RemoveConfirmation_OnDemand_Message_1 + Environment.NewLine + Environment.NewLine +
        " \u25cf " + Resources.Strings.Main_MyComputer_Folders_RemoveConfirmation_OnDemand_Message_Item_1 + Environment.NewLine +
        " \u25cf " + Resources.Strings.Main_MyComputer_Folders_RemoveConfirmation_OnDemand_Message_Item_2 + Environment.NewLine + Environment.NewLine +
        Resources.Strings.Main_MyComputer_Folders_RemoveConfirmation_OnDemand_Message_2;

    public RemoveOnDemandFolderConfirmationViewModel()
        : base(message: ContentText)
    {
        IsConfirmingDangerousAction = true;
    }
}
