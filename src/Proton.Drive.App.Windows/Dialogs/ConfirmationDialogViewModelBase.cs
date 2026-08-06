using CommunityToolkit.Mvvm.Input;
using Proton.Drive.App.Windows.Views;

namespace Proton.Drive.App.Windows.Dialogs;

public abstract class ConfirmationDialogViewModelBase : IDialogViewModel
{
    protected ConfirmationDialogViewModelBase(string title, string message)
    {
        Title = title;
        Message = message;

        ConfirmAndCloseCommand = new RelayCommand<IClosableDialog>(ConfirmAndClose);
    }

    public string Title { get; }
    public string Message { get; protected set; }

    public string ConfirmButtonText { get; protected set; } = Resources.Strings.Dialog_Button_Ok;
    public string CancelButtonText { get; protected set; } = Resources.Strings.Dialog_Button_Cancel;

    public bool IsConfirmingDangerousAction { get; protected set; }
    public bool IsCancelButtonVisible { get; protected set; } = true;

    public RelayCommand<IClosableDialog> ConfirmAndCloseCommand { get; }

    private static void ConfirmAndClose(IClosableDialog? dialog)
    {
        if (dialog is null)
        {
            return;
        }

        dialog.DialogResult = true;
        dialog.Close();
    }
}
