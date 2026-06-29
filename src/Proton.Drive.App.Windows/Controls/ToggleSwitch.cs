using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Proton.Drive.App.Windows.Controls;

/// <summary>
/// Light implementation of a switch toggle button
/// </summary>
internal sealed class ToggleSwitch : ToggleButton
{
    private const string DefaultOnContent = "";
    private const string DefaultOffContent = "";

    public static readonly DependencyProperty ContentPlacementProperty = DependencyProperty.Register(
        nameof(ContentPlacement),
        typeof(ToggleSwitchContentPlacement),
        typeof(ToggleSwitch),
        new PropertyMetadata(ToggleSwitchContentPlacement.Right));

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
        ToolTipService.ShowOnDisabledProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(true));
    }

    public ToggleSwitchContentPlacement ContentPlacement
    {
        get => (ToggleSwitchContentPlacement)GetValue(ContentPlacementProperty);
        set => SetValue(ContentPlacementProperty, value);
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
