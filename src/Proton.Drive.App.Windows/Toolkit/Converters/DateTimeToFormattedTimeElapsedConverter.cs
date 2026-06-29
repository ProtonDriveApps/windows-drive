using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Proton.Drive.App.Windows.Resources;

namespace Proton.Drive.App.Windows.Toolkit.Converters;

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
            < 1 => Strings.Main_Activity_TimeElapsed_AMomentAgo,
            < 2 => Strings.Main_Activity_TimeElapsed_OneMinuteAgo,
            < 60 => string.Format(Strings.Main_Activity_TimeElapsed_MinutesAgoFormat, minutesElapsed),
            < 60 * 2 => Strings.Main_Activity_TimeElapsed_OneHourAgo,
            < 60 * 24 => string.Format(Strings.Main_Activity_TimeElapsed_HoursAgoFormat, minutesElapsed / 60),
            < 60 * 24 * 2 => Strings.Main_Activity_TimeElapsed_OneDayAgo,
            _ => string.Format(Strings.Main_Activity_TimeElapsed_DaysAgoFormat, minutesElapsed / 60 / 24),
        };
    }
}
