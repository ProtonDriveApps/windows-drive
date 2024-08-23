using System;
using System.Globalization;
using System.Windows.Data;

namespace ProtonDrive.App.Windows.Toolkit.Converters;

[ValueConversion(typeof(object), typeof(bool))]
internal sealed class ObjectEqualityToBooleanConverter : IValueConverter
{
    private static ObjectEqualityToBooleanConverter? _equalIsTrue;
    private static ObjectEqualityToBooleanConverter? _equalIsFalse;

    public static ObjectEqualityToBooleanConverter EqualIsTrue
        => _equalIsTrue ??= new ObjectEqualityToBooleanConverter { TrueValue = true };

    public static ObjectEqualityToBooleanConverter EqualIsFalse
        => _equalIsFalse ??= new ObjectEqualityToBooleanConverter { TrueValue = false };

    public bool TrueValue { get; init; } = true;

    public object Convert(object? value, Type? targetType, object? parameter, CultureInfo? culture)
    {
        return Equals(value, parameter) ? TrueValue : !TrueValue;
    }

    public object? ConvertBack(object? value, Type? targetType, object? parameter, CultureInfo? culture)
    {
        return Equals(value, TrueValue)
            ? parameter
            : Binding.DoNothing;
    }
}
