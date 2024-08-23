using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProtonDrive.App.Windows.Toolkit.Converters;

public sealed class EnumToDisplayTitleTextConverter : IValueConverter
{
    private static EnumToDisplayTitleTextConverter? _instance;

    public static EnumToDisplayTitleTextConverter Instance => _instance ??= new EnumToDisplayTitleTextConverter();

    public object? Convert(object? value, Type? targetType, object? parameter, CultureInfo? culture)
    {
        if (value == null)
        {
            return DependencyProperty.UnsetValue;
        }

        var sourceType = value.GetType();
        var valueName = Enum.GetName(sourceType, value) ?? string.Empty;
        var key = $"{sourceType.Name}_ttl_{valueName}";

        return Resources.Strings.ResourceManager.GetString(key) ?? DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object? value, Type? targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotSupportedException();
    }
}
