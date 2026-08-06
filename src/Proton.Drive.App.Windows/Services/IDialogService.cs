using Proton.Drive.App.Windows.Dialogs;
using Proton.Drive.App.Windows.Views;

namespace Proton.Drive.App.Windows.Services;

internal interface IDialogService
{
    ConfirmationResult ShowConfirmationDialog(ConfirmationDialogViewModelBase dataContext);
    void Show(IDialogViewModel dataContext);
    void ShowDialog(IDialogViewModel dataContext);
    void ShowHumanVerificationDialog(IDialogViewModel dataContext);
}
