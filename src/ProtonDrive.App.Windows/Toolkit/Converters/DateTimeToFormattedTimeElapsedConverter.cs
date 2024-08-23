using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProtonDrive.App.Windows.Toolkit.Converters;

[ValueConversion(typeof(DateTime), typeof(string))]
internal class DateTimeToFormattedTimeElapsedConverter : IValueConverter
{
    private const string DefaultFormattedElapsedTime = "...";

    private static DateTimeToFormattedTimeElapsedConverter? _instance;

    public static DateTimeToFormattedTimeElapsedConverter Instance => _instance ??= new DateTimeToFormattedTimeElapsedConverter();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            DateTime dateTime => Convert(dateTime),
            null => Convert(null),
            _ => DependencyProperty.UnsetValue,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static string Convert(DateTime? dateTime)
    {
        var now = DateTime.UtcNow;

        if (dateTime is null)
        {
            return DefaultFormattedElapsedTime;
        }

        var minutesElapsed = (int)(now - dateTime).Value.TotalMinutes;

        return minutesElapsed switch
        {
            < 2 => "a minute ago",
            < 60 => $"{minutesElapsed} minutes ago",
            < 60 * 2 => "an hour ago",
            < 60 * 24 => $"{minutesElapsed / 60} hours ago",
            < 60 * 24 * 2 => "a day ago",
            _ => $"{minutesElapsed / 60 / 24} days ago",
        };
    }
}
