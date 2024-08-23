namespace ProtonDrive.App.Windows.Dialogs;

public interface IClosableDialog
{
    bool? DialogResult { get; set; }

    void Close();
}
