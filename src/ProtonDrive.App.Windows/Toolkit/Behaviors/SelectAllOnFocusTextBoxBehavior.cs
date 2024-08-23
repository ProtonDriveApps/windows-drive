using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace ProtonDrive.App.Windows.Toolkit.Behaviors;

internal sealed class SelectAllOnFocusTextBoxBehavior : Behavior<Control>
{
    protected override void OnAttached()
    {
        base.OnAttached();

        AssociatedObject.GotKeyboardFocus += OnGotKeyboardFocus;
        AssociatedObject.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        AssociatedObject.GotKeyboardFocus -= OnGotKeyboardFocus;
        AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
    }

    private void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        SelectAll();
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (AssociatedObject.IsKeyboardFocusWithin)
        {
            return;
        }

        AssociatedObject.Focus();
        e.Handled = true;
    }

    private void SelectAll()
    {
        if (AssociatedObject is TextBoxBase textBox)
        {
            textBox.SelectAll();
        }
        else if (AssociatedObject is PasswordBox passwordBox)
        {
            passwordBox.SelectAll();
        }
    }
}
