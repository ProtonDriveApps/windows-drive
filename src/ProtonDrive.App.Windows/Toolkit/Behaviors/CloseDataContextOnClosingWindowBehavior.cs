using System.ComponentModel;
using System.Windows;
using Microsoft.Xaml.Behaviors;
using ProtonDrive.App.Windows.Views.Shared;

namespace ProtonDrive.App.Windows.Toolkit.Behaviors;

internal sealed class CloseDataContextOnClosingWindowBehavior : Behavior<Window>
{
    protected override void OnAttached()
    {
        base.OnAttached();

        AssociatedObject.Closing += OnClosing;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        AssociatedObject.Closing -= OnClosing;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (AssociatedObject.DataContext is not ICloseable closeable)
        {
            return;
        }

        closeable.Close();
    }
}
