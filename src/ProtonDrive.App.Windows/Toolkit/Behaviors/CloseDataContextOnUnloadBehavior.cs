using System.Windows;
using Microsoft.Xaml.Behaviors;
using ProtonDrive.App.Windows.Views.Shared;

namespace ProtonDrive.App.Windows.Toolkit.Behaviors;

internal sealed class CloseDataContextOnUnloadBehavior : Behavior<FrameworkElement>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Unloaded += OnUnload;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.Unloaded -= OnUnload;
    }

    private void OnUnload(object sender, RoutedEventArgs e)
    {
        if (AssociatedObject.DataContext is not ICloseable closeable)
        {
            return;
        }

        closeable.Close();
    }
}
