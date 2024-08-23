using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProtonDrive.App.Windows.Toolkit.Converters;

[ValueConversion(typeof(double), typeof(Thickness))]
internal sealed class NumberToUniformThicknessConverter : IValueConverter
{
    private static readonly Lazy<NumberToUniformThicknessConverter> LazyInstance = new(() => new NumberToUniformThicknessConverter());

    private NumberToUniformThicknessConverter()
    {
    }

    public static IValueConverter Instance => LazyInstance.Value;

    public static Thickness Convert(double number)
    {
        return new(number);
    }

    public static double ConvertBack(Thickness thickness)
    {
        return (thickness.Left + thickness.Top + thickness.Right + thickness.Bottom) / 4d;
    }

    object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var number = System.Convert.ToDouble(value);
        return Convert(number);
    }

    object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Thickness thickness
            ? ConvertBack(thickness)
            : Binding.DoNothing;
    }
}
