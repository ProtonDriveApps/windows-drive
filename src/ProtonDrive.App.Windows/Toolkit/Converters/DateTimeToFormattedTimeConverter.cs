using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProtonDrive.App.Windows.Toolkit.Converters;

[ValueConversion(typeof(DateTime), typeof(string))]
internal class DateTimeToFormattedTimeConverter : IValueConverter
{
    private static DateTimeToFormattedTimeConverter? _instance;

    public static DateTimeToFormattedTimeConverter Instance => _instance ??= new DateTimeToFormattedTimeConverter();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            DateTime dateTime => Convert(dateTime, culture),
            _ => DependencyProperty.UnsetValue,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static string Convert(DateTime dateTime, CultureInfo culture)
    {
        return dateTime.ToLocalTime().ToString("G", culture);
    }
}
