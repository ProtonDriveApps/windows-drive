using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Proton.Drive.App.Windows.Resources;

namespace Proton.Drive.App.Windows.Toolkit.Converters;

public sealed class EnumToDisplayTextConverter : IValueConverter
{
    public const string TypeNamePlaceholder = "{Type}";
    public const string ValueNamePlaceholder = "{Value}";

    private static EnumToDisplayTextConverter? _instance;

    public static EnumToDisplayTextConverter Instance => _instance ??= new EnumToDisplayTextConverter();

    public static string? Convert(object value, object? parameter = null, CultureInfo? culture = null)
    {
        var sourceType = value.GetType();
        var valueName = Enum.GetName(sourceType, value) ?? string.Empty;
        var key = parameter is string pattern
            ? GetResourceKey(pattern, sourceType.Name, valueName)
            : $"{sourceType.Name}_Value_{valueName}";

        if (culture?.Equals(CultureInfo.CurrentCulture) == true)
        {
            culture = Strings.Culture;
        }

        return Strings.ResourceManager.GetString(key, culture ?? Strings.Culture);
    }

    object? IValueConverter.Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value == null
            ? DependencyProperty.UnsetValue
            : Convert(value, parameter, culture);
    }

    object IValueConverter.ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static string GetResourceKey(string pattern, string typeName, string valueName)
    {
        return pattern
            .Replace(TypeNamePlaceholder, typeName)
            .Replace(ValueNamePlaceholder, valueName);
    }
}
