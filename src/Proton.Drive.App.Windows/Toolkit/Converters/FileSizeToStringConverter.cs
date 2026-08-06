using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Proton.Drive.App.Windows.Toolkit.Converters;

[ValueConversion(typeof(long), typeof(string))]
internal class FileSizeToStringConverter : IValueConverter
{
    private static readonly string[] Suffixes = [" B", " KB", " MB", " GB", " TB", " PB"];

    private static FileSizeToStringConverter? _instance;

    public static FileSizeToStringConverter Instance => _instance ??= new FileSizeToStringConverter();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not long bytes
            ? DependencyProperty.UnsetValue
            : Convert(bytes);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static string Convert(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes}" + Suffixes[0];
        }

        var magnitude = (int)Math.Log(bytes, 1024);
        var num = Math.Round(bytes / (decimal)Math.Pow(1024, magnitude), 2);
        return num + Suffixes[magnitude];
    }
}
