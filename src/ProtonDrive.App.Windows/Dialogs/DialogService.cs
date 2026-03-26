using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Wpf;
using ProtonDrive.App.Windows.Dialogs.HumanVerification;
using ProtonDrive.App.Windows.Services;
using ProtonDrive.App.Windows.Views;
using ProtonDrive.Shared.Configuration;

namespace ProtonDrive.App.Windows.Dialogs;

internal sealed class DialogService(AppConfig appConfig, App app, ILoggerFactory loggerFactory) : IDialogService
{
    public ConfirmationResult ShowConfirmationDialog(ConfirmationDialogViewModelBase dataContext)
    {
        var confirmationDialog = new ConfirmationDialogWindow
        {
            DataContext = dataContext,
            Owner = app.GetActiveWindow(),
        };

        var result = confirmationDialog.ShowDialog();

        if (result is null)
        {
            return ConfirmationResult.Cancelled;
        }

        return result.GetValueOrDefault() ? ConfirmationResult.Confirmed : ConfirmationResult.Cancelled;
    }

    public void Show(IDialogViewModel dataContext)
    {
        var dialog = new DialogWindow
        {
            DataContext = dataContext,
            Owner = app.GetActiveWindow(),
        };

        dialog.Show();
    }

    public void ShowDialog(IDialogViewModel dataContext)
    {
        var dialog = new DialogWindow
        {
            DataContext = dataContext,
            Owner = app.GetActiveWindow(),
        };

        dialog.ShowDialog();
    }

    public void ShowHumanVerificationDialog(IDialogViewModel dataContext)
    {
        var properties = new CoreWebView2CreationProperties
        {
            UserDataFolder = appConfig.WebView2DataPath,
        };

        var dialog = new HumanVerificationDialogWindow(properties, loggerFactory.CreateLogger<HumanVerificationDialogWindow>())
        {
            DataContext = dataContext,
            Owner = app.GetActiveWindow(),
        };

        dialog.ShowDialog();
    }
}
