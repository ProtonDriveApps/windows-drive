using CommunityToolkit.Mvvm.ComponentModel;

namespace Proton.Drive.App.Windows.Views.Main;

internal abstract class PageViewModel : ObservableObject
{
    internal virtual void OnActivated()
    {
    }
}
