using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProtonDrive.App.Windows.Toolkit.Converters;

[ValueConversion(typeof(double), typeof(GridLength))]
internal sealed class DoubleToGridLengthConverter : IValueConverter
{
    private static readonly Lazy<DoubleToGridLengthConverter> LazyInstance = new(() => new DoubleToGridLengthConverter());

    private DoubleToGridLengthConverter()
    {
    }

    public static IValueConverter Instance => LazyInstance.Value;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is double d ? new GridLength(d) : value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is GridLength length ? length.Value : value;
    }
}
