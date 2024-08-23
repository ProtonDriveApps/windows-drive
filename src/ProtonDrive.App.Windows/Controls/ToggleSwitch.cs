using System.Windows;
using System.Windows.Controls.Primitives;

namespace ProtonDrive.App.Windows.Controls;

/// <summary>
/// Light implementation of a switch toggle button
/// </summary>
public class ToggleSwitch : ToggleButton
{
    // TODO: Localization
    private const string DefaultOnContent = "On";
    private const string DefaultOffContent = "Off";

    public static readonly DependencyProperty OnContentProperty = DependencyProperty.Register(
        nameof(OnContent),
        typeof(object),
        typeof(ToggleSwitch),
        new PropertyMetadata(DefaultOnContent));

    public static readonly DependencyProperty OffContentProperty = DependencyProperty.Register(
        nameof(OffContent),
        typeof(object),
        typeof(ToggleSwitch),
        new PropertyMetadata(DefaultOffContent));

    static ToggleSwitch()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ToggleSwitch), new FrameworkPropertyMetadata(typeof(ToggleSwitch)));
    }

    public object OnContent
    {
        get => GetValue(OnContentProperty);
        set => SetValue(OnContentProperty, value);
    }

    public object OffContent
    {
        get => GetValue(OffContentProperty);
        set => SetValue(OffContentProperty, value);
    }
}
