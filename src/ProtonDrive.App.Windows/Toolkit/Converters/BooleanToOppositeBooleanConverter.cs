using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProtonDrive.App.Windows.Toolkit.Converters;

/// <summary>
/// Convert a boolean value to its opposite boolean value
/// </summary>
[ValueConversion(typeof(bool), typeof(bool))]
public sealed class BooleanToOppositeBooleanConverter : IValueConverter
{
    private static BooleanToOppositeBooleanConverter? _instance;

    public static BooleanToOppositeBooleanConverter Instance => _instance ??= new BooleanToOppositeBooleanConverter();

    public static bool Convert(bool value)
    {
        return !value;
    }

    object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not bool flag)
        {
            return DependencyProperty.UnsetValue;
        }

        return Convert(flag);
    }

    object? IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return ((IValueConverter)this).Convert(value, targetType, parameter, culture);
    }
}
