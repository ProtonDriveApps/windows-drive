using Proton.Drive.App.Windows.Dialogs;

namespace Proton.Drive.App.Windows.Views.Main.MyComputer;

internal abstract class RemoveFolderConfirmationViewModelBase : ConfirmationDialogViewModelBase
{
    private readonly string _message;

    protected RemoveFolderConfirmationViewModelBase(string message)
        : base(Resources.Strings.Main_MyComputer_Folders_RemoveConfirmation_Title, message)
    {
        _message = message;
        ConfirmButtonText = Resources.Strings.Main_MyComputer_Folders_RemoveConfirmation_Button_Confirm;
    }

    public void SetFolderName(string folderName)
    {
        Message = string.Format(_message, folderName);
    }
}
