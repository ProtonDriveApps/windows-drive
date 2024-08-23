using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProtonDrive.App.Windows.Toolkit.Converters;

/// <summary>
/// Convert a boolean to a visibility
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    private static BooleanToVisibilityConverter? _falseIsCollapsed;
    private static BooleanToVisibilityConverter? _falseIsHidden;
    private static BooleanToVisibilityConverter? _trueIsCollapsed;
    private static BooleanToVisibilityConverter? _trueIsHidden;

    public static BooleanToVisibilityConverter FalseIsCollapsed
        => _falseIsCollapsed ??= new() { InvisibilityValue = Visibility.Collapsed };

    public static BooleanToVisibilityConverter FalseIsHidden
        => _falseIsHidden ??= new() { InvisibilityValue = Visibility.Hidden };

    public static BooleanToVisibilityConverter TrueIsCollapsed
        => _trueIsCollapsed ??= new() { BooleanValueForVisibility = false, InvisibilityValue = Visibility.Collapsed };

    public static BooleanToVisibilityConverter TrueIsHidden
        => _trueIsHidden ??= new() { BooleanValueForVisibility = false, InvisibilityValue = Visibility.Hidden };

    public bool BooleanValueForVisibility { get; set; } = true;

    public Visibility InvisibilityValue { get; set; } = Visibility.Collapsed;

    public Visibility Convert(bool value)
    {
        return value == BooleanValueForVisibility ? Visibility.Visible : InvisibilityValue;
    }

    object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is not bool booleanValue ? DependencyProperty.UnsetValue : Convert(booleanValue);
    }

    object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
