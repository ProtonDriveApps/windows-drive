using System.Windows;
using Microsoft.Xaml.Behaviors;

namespace ProtonDrive.App.Windows.Toolkit.Behaviors;

internal class VisibilityChangeNotificationBehavior : Behavior<FrameworkElement>
{
    protected override void OnAttached()
    {
        AssociatedObject.IsVisibleChanged += OnIsVisibleChanged;

        base.OnAttached();
    }

    protected override void OnDetaching()
    {
        AssociatedObject.IsVisibleChanged -= OnIsVisibleChanged;

        base.OnDetaching();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!AssociatedObject.IsVisible || AssociatedObject.DataContext is not IVisibilityListener listener)
        {
            return;
        }

        listener.OnVisibilityChanged(AssociatedObject.IsVisible);
    }
}
