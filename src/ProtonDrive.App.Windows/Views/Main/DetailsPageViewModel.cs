using System;
using ProtonDrive.App.Windows.Views.Shared.Navigation;

namespace ProtonDrive.App.Windows.Views.Main;

internal abstract class DetailsPageViewModel : INavigatablePage
{
    public event EventHandler? Closed;

    public string Title { get; protected set; } = string.Empty;

    public virtual void OnActivated()
    {
    }

    public virtual void Close()
    {
        OnClosed();
    }

    protected virtual void OnClosed()
    {
        Closed?.Invoke(this, EventArgs.Empty);
    }
}
