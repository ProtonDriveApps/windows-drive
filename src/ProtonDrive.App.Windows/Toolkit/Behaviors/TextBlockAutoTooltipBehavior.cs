using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Xaml.Behaviors;

namespace ProtonDrive.App.Windows.Toolkit.Behaviors;

public sealed class TextBlockAutoToolTipBehavior : Behavior<TextBlock>
{
    private static readonly DependencyProperty Text =
        DependencyProperty.Register(
            "Text",
            typeof(string),
            typeof(TextBlockAutoToolTipBehavior),
            new UIPropertyMetadata(defaultValue: null, OnTextChanged));

    protected override void OnAttached()
    {
        base.OnAttached();

        AssociatedObject.TextTrimming = TextTrimming.CharacterEllipsis;

        BindingOperations.SetBinding(this, Text, new Binding
        {
            Source = AssociatedObject,
            Path = new PropertyPath(TextBlock.TextProperty),
            Mode = BindingMode.OneWay,
        });

        AssociatedObject.SizeChanged += OnAssociatedObjectSizeChanged;

        HandleToolTipChange();
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        BindingOperations.ClearAllBindings(this);

        AssociatedObject.SizeChanged -= OnAssociatedObjectSizeChanged;
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlockAutoToolTipBehavior behavior)
        {
            behavior.HandleToolTipChange();
        }
    }

    private void OnAssociatedObjectSizeChanged(object? sender, SizeChangedEventArgs sizeChangedEventArgs)
    {
        HandleToolTipChange();
    }

    private void HandleToolTipChange()
    {
        if (AssociatedObject.IsLoaded)
        {
            HandleTooltip();
        }
        else
        {
            AssociatedObject.Dispatcher.BeginInvoke(HandleTooltip, DispatcherPriority.Loaded);
        }

        void HandleTooltip()
        {
            AssociatedObject.ToolTip = IsTextTrimmed(AssociatedObject) ? AssociatedObject.Text : null;
        }
    }

    private bool IsTextTrimmed(TextBlock textBlock)
    {
        var typeface = new Typeface(
            textBlock.FontFamily,
            textBlock.FontStyle,
            textBlock.FontWeight,
            textBlock.FontStretch);

        // FormattedText is used to measure the size required to display the text
        // contained in the TextBlock control.
        var formattedText = new FormattedText(
            textBlock.Text,
            System.Threading.Thread.CurrentThread.CurrentCulture,
            textBlock.FlowDirection,
            typeface,
            textBlock.FontSize,
            textBlock.Foreground,
            VisualTreeHelper.GetDpi(textBlock).PixelsPerDip)
        {
            MaxTextWidth = textBlock.ActualWidth,
        };

        // If the textBlock is being trimmed to fit then the formatted text will report
        // a larger height than the textBlock. The width of the formattedText might grow
        // if a single line is too long to fit within the text area, this can only happen
        // if there is a long text with no spaces.
        return formattedText.Height > textBlock.ActualHeight || formattedText.MinWidth > textBlock.ActualWidth;
    }
}
