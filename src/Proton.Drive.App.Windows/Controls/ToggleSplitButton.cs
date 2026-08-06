using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Proton.Drive.App.Windows.Controls;

/// <summary>
/// A toggle button with a secondary button that opens a drop-down menu.
/// Clicking the main button toggles <see cref="ToggleButton.IsChecked"/> (the primary action);
/// clicking the secondary button opens the <see cref="DropDownMenu"/>.
/// </summary>
[TemplatePart(Name = "PART_DropDownButton", Type = typeof(ButtonBase))]
internal sealed class ToggleSplitButton : ToggleButton
{
    public static readonly DependencyProperty DropDownMenuProperty = DependencyProperty.Register(
        nameof(DropDownMenu),
        typeof(ContextMenu),
        typeof(ToggleSplitButton),
        new PropertyMetadata(null, OnDropDownMenuChanged));

    private const string DropDownButtonPartName = "PART_DropDownButton";

    private static readonly TimeSpan MinimumDelayBetweenCloseAndReopen = TimeSpan.FromMilliseconds(150);

    private ButtonBase? _dropDownButton;
    private DateTime _dropDownClosedAt = DateTime.MinValue;

    static ToggleSplitButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ToggleSplitButton), new FrameworkPropertyMetadata(typeof(ToggleSplitButton)));
    }

    public ContextMenu? DropDownMenu
    {
        get => (ContextMenu?)GetValue(DropDownMenuProperty);
        set => SetValue(DropDownMenuProperty, value);
    }

    public override void OnApplyTemplate()
    {
        if (_dropDownButton is not null)
        {
            _dropDownButton.Click -= OnDropDownButtonClick;
        }

        base.OnApplyTemplate();

        _dropDownButton = GetTemplateChild(DropDownButtonPartName) as ButtonBase;

        if (_dropDownButton is not null)
        {
            _dropDownButton.Click += OnDropDownButtonClick;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var pressedKey = e.Key == Key.System ? e.SystemKey : e.Key;

        if (pressedKey == Key.Down && OpenDropDown())
        {
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private static void OnDropDownMenuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ToggleSplitButton toggleSplitButton)
        {
            return;
        }

        if (e.OldValue is ContextMenu oldMenu)
        {
            oldMenu.Closed -= toggleSplitButton.OnDropDownMenuClosed;
        }

        if (e.NewValue is ContextMenu newMenu)
        {
            newMenu.Closed += toggleSplitButton.OnDropDownMenuClosed;
        }
    }

    private void OnDropDownMenuClosed(object sender, RoutedEventArgs e)
    {
        _dropDownClosedAt = DateTime.UtcNow;
    }

    private void OnDropDownButtonClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        OpenDropDown();
    }

    private bool OpenDropDown()
    {
        if (DropDownMenu is null)
        {
            return false;
        }

        // A request to open the menu that happens within this delay of it closing is ignored.
        // The menu closes itself on the mouse-down, so without this the following mouse-up would reopen it.
        if (DateTime.UtcNow - _dropDownClosedAt <= MinimumDelayBetweenCloseAndReopen)
        {
            return false;
        }

        DropDownMenu.PlacementTarget = this;
        DropDownMenu.Placement = PlacementMode.Bottom;
        DropDownMenu.IsOpen = true;

        return true;
    }
}
