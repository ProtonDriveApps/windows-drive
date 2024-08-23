using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace ProtonDrive.App.Windows.Toolkit.Converters;

internal class LastColumnSizeCalculatingConverter : IMultiValueConverter
{
    private static LastColumnSizeCalculatingConverter? _instance;

    public static LastColumnSizeCalculatingConverter Instance => _instance ??= new LastColumnSizeCalculatingConverter();

    public static double? Convert(double firstValue, IEnumerable<object?> remainingValues)
    {
        var result = remainingValues.Aggregate(firstValue, (current, remainingValue) => current - (remainingValue as double? ?? 0));

        return result >= 0 ? result : null;
    }

    public object Convert(object?[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
        {
            return DependencyProperty.UnsetValue;
        }

        return Convert(values[0] as double? ?? 0, values.Skip(1)) ?? DependencyProperty.UnsetValue;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
