using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProtonDrive.App.Windows.Toolkit.Converters;

[ValueConversion(typeof(string), typeof(string))]
internal class CapitalizationConverter : IValueConverter
{
    private static CapitalizationConverter? _instance;

    public static CapitalizationConverter Instance => _instance ??= new CapitalizationConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string text)
        {
            return DependencyProperty.UnsetValue;
        }

        return Convert(text);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static string Convert(string text)
    {
        return text.Length switch
        {
            0 or 1 => text.ToUpper(),
            _ => char.ToUpper(text[0]) + text[1..],
        };
    }
}
