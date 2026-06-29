using System.Windows;
using Microsoft.Xaml.Behaviors;

namespace Proton.Drive.App.Windows.Toolkit.Behaviors;

internal sealed class CenterWindowOnSizeChangedBehavior : Behavior<Window>
{
    protected override void OnAttached()
    {
        base.OnAttached();

        AssociatedObject.SizeChanged += OnSizeChanged;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        AssociatedObject.SizeChanged -= OnSizeChanged;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (AssociatedObject.WindowStartupLocation is not WindowStartupLocation.CenterScreen)
        {
            return;
        }

        // The first event reports a zero previous size.
        if (e.PreviousSize.Width == 0 || e.PreviousSize.Height == 0)
        {
            return;
        }

        // The window has not been positioned yet.
        if (double.IsNaN(AssociatedObject.Left) || double.IsNaN(AssociatedObject.Top))
        {
            return;
        }

        AssociatedObject.Left -= (e.NewSize.Width - e.PreviousSize.Width) / 2;
        AssociatedObject.Top -= (e.NewSize.Height - e.PreviousSize.Height) / 2;
    }
}
