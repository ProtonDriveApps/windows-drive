using CommunityToolkit.Mvvm.ComponentModel;

namespace ProtonDrive.App.Windows.Views.Main;

internal abstract class PageViewModel : ObservableObject
{
    internal virtual void OnActivated()
    {
    }
}
