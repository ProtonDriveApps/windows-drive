using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProtonDrive.App.Windows.Toolkit.Converters;

[ValueConversion(typeof(object), typeof(Visibility))]
internal sealed class ObjectEqualityToVisibilityConverter : IValueConverter
{
    private static ObjectEqualityToVisibilityConverter? _equalIsHidden;
    private static ObjectEqualityToVisibilityConverter? _equalIsCollapsed;
    private static ObjectEqualityToVisibilityConverter? _notEqualIsHidden;
    private static ObjectEqualityToVisibilityConverter? _notEqualIsCollapsed;

    public static ObjectEqualityToVisibilityConverter EqualIsHidden
        => _equalIsHidden ??= new ObjectEqualityToVisibilityConverter { FalseValue = Visibility.Visible, TrueValue = Visibility.Hidden };

    public static ObjectEqualityToVisibilityConverter EqualIsCollapsed
        => _equalIsCollapsed ??= new ObjectEqualityToVisibilityConverter { FalseValue = Visibility.Visible, TrueValue = Visibility.Collapsed };

    public static ObjectEqualityToVisibilityConverter NotEqualIsHidden
        => _notEqualIsHidden ??= new ObjectEqualityToVisibilityConverter { FalseValue = Visibility.Hidden };

    public static ObjectEqualityToVisibilityConverter NotEqualIsCollapsed
        => _notEqualIsCollapsed ??= new ObjectEqualityToVisibilityConverter { FalseValue = Visibility.Collapsed };

    public Visibility FalseValue { get; init; } = Visibility.Hidden;
    public Visibility TrueValue { get; set; }

    public object Convert(object? value, Type? targetType, object? parameter, CultureInfo? culture)
    {
        return Equals(value, parameter) ? TrueValue : FalseValue;
    }

    public object ConvertBack(object? value, Type? targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotSupportedException();
    }
}
