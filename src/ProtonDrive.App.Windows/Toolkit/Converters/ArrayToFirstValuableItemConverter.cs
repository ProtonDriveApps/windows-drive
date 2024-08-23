using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProtonDrive.App.Windows.Toolkit.Converters;

internal class ArrayToFirstValuableItemConverter : IMultiValueConverter
{
    private static ArrayToFirstValuableItemConverter? _instance;

    public static ArrayToFirstValuableItemConverter Instance => _instance ??= new ArrayToFirstValuableItemConverter();

    public object Convert(object?[] values, Type targetType, object parameter, CultureInfo culture)
    {
        return Array.Find(values, value => value != null && value != DependencyProperty.UnsetValue && value != Binding.DoNothing)
               ?? DependencyProperty.UnsetValue;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
