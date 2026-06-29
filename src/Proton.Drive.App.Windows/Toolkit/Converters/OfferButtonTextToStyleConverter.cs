using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Proton.Drive.App.Windows.Toolkit.Converters;

[ValueConversion(typeof(string), typeof(Style))]
internal sealed class OfferButtonTextToStyleConverter : IValueConverter
{
    private const string PromotionPrefixText = "%";

    private static OfferButtonTextToStyleConverter? _instance;

    public static OfferButtonTextToStyleConverter Instance => _instance ??= new OfferButtonTextToStyleConverter();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text)
        {
            return DependencyProperty.UnsetValue;
        }

        return Convert(text) ?? DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static Style? Convert(string text)
    {
        var styleKey = text.StartsWith(PromotionPrefixText) ? "OfferButtonStyle.Promo" : "OfferButtonStyle";

        return Application.Current.TryFindResource(styleKey) as Style;
    }
}
