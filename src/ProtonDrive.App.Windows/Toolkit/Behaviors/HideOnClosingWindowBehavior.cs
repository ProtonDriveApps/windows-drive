using System.ComponentModel;
using System.Windows;
using Microsoft.Xaml.Behaviors;

namespace ProtonDrive.App.Windows.Toolkit.Behaviors;

internal sealed class HideOnClosingWindowBehavior : Behavior<Window>
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
        e.Cancel = true;
        AssociatedObject.Hide();
    }
}
