using CommunityToolkit.Mvvm.Input;
using ProtonDrive.App.Windows.Views;

namespace ProtonDrive.App.Windows.Dialogs;

public abstract class ConfirmationDialogViewModelBase : IDialogViewModel
{
    protected ConfirmationDialogViewModelBase(string title, string message)
    {
        Title = title;
        Message = message;
        ConfirmAndCloseCommand = new RelayCommand<IClosableDialog>(ConfirmAndClose);
    }

    protected ConfirmationDialogViewModelBase(string title, string message, string confirmButtonText, string cancelButtonText)
        : this(title, message)
    {
        ConfirmButtonText = confirmButtonText;
        CancelButtonText = cancelButtonText;
    }

    public string? Title { get; }
    public string Message { get; set; }

    public string ConfirmButtonText { get; protected set; } = "Ok";
    public string CancelButtonText { get; protected set; } = "Cancel";

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
