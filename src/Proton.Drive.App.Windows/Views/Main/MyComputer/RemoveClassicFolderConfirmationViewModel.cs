namespace Proton.Drive.App.Windows.Views.Main.MyComputer;

internal sealed class RemoveClassicFolderConfirmationViewModel : RemoveFolderConfirmationViewModelBase
{
    private static readonly string ContentText =
        Resources.Strings.Main_MyComputer_Folders_RemoveConfirmation_Classic_Message_1 + Environment.NewLine +
        Resources.Strings.Main_MyComputer_Folders_RemoveConfirmation_Classic_Message_2;

    public RemoveClassicFolderConfirmationViewModel()
        : base(message: ContentText)
    {
    }
}
