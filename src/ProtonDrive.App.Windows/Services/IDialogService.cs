using ProtonDrive.App.Windows.Dialogs;
using ProtonDrive.App.Windows.Views;

namespace ProtonDrive.App.Windows.Services;

internal interface IDialogService
{
    ConfirmationResult ShowConfirmationDialog(ConfirmationDialogViewModelBase dataContext);

    void Show(IDialogViewModel dataContext);

    void ShowDialog(IDialogViewModel dataContext);
}
