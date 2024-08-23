using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace ProtonDrive.App.Windows.Toolkit.Behaviors;

internal sealed class ClearFocusOnMouseDownBehavior : Behavior<Window>
{
    protected override void OnAttached()
    {
        base.OnAttached();

        AssociatedObject.MouseDown += OnMouseDown;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        AssociatedObject.MouseDown -= OnMouseDown;
    }

    private static void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        Keyboard.ClearFocus();
    }
}
