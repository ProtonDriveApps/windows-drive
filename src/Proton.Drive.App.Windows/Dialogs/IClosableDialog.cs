namespace Proton.Drive.App.Windows.Dialogs;

public interface IClosableDialog
{
    bool? DialogResult { get; set; }

    void Close();
}
