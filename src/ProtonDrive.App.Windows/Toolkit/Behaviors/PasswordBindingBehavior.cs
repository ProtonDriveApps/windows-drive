using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Xaml.Behaviors;

namespace ProtonDrive.App.Windows.Toolkit.Behaviors;

public class PasswordBindingBehavior : Behavior<PasswordBox>
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.RegisterAttached(
        nameof(SetValue)[3..],
        typeof(SecureString),
        typeof(PasswordBindingBehavior),
        new FrameworkPropertyMetadata(default(SecureString), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static void SetValue(DependencyObject element, SecureString value)
    {
        element.SetValue(ValueProperty, value);
    }

    public static SecureString GetValue(DependencyObject element)
    {
        return (SecureString)element.GetValue(ValueProperty);
    }

    private Binding? _binding;

    public Binding? Binding
    {
        get => _binding;
        set
        {
            if (_binding == value)
            {
                return;
            }

            OnBindingChanged(_binding, value);
            _binding = value;
        }
    }

    protected override void OnAttached()
    {
        if (Binding is not null)
        {
            AssociatedObject.SetBinding(ValueProperty, Binding);
        }

        AssociatedObject.PasswordChanged += OnPasswordChanged;

        base.OnAttached();
    }

    protected override void OnDetaching()
    {
        var existingBinding = BindingOperations.GetBindingBase(AssociatedObject, ValueProperty);
        if (Binding is not null && existingBinding == Binding)
        {
            BindingOperations.ClearBinding(AssociatedObject, ValueProperty);
        }

        AssociatedObject.PasswordChanged -= OnPasswordChanged;

        base.OnDetaching();
    }

    private void OnBindingChanged(Binding? oldBinding, Binding? newBinding)
    {
        if (AssociatedObject is null)
        {
            return;
        }

        var existingBinding = BindingOperations.GetBindingBase(AssociatedObject, ValueProperty);

        if (newBinding is not null)
        {
            AssociatedObject.SetBinding(ValueProperty, newBinding);
        }
        else
        {
            if (oldBinding == existingBinding)
            {
                BindingOperations.ClearBinding(AssociatedObject, ValueProperty);
            }
        }
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        AssociatedObject.SetCurrentValue(ValueProperty, AssociatedObject.SecurePassword);
    }
}
