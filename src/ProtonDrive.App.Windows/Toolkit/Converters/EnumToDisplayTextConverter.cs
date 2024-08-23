using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProtonDrive.App.Windows.Toolkit.Converters;

public sealed class EnumToDisplayTextConverter : IValueConverter
{
    public const string TypeNamePlaceholder = "{Type}";
    public const string ValueNamePlaceholder = "{Value}";

    private static EnumToDisplayTextConverter? _instance;

    public static EnumToDisplayTextConverter Instance => _instance ??= new EnumToDisplayTextConverter();

    public object? Convert(object? value, Type? targetType, object? parameter, CultureInfo? culture)
    {
        if (value == null)
        {
            return DependencyProperty.UnsetValue;
        }

        var sourceType = value.GetType();
        var valueName = Enum.GetName(sourceType, value) ?? string.Empty;
        var key = parameter is string pattern
            ? GetResourceKey(pattern, sourceType.Name, valueName)
            : $"{sourceType.Name}_val_{valueName}";

        return Resources.Strings.ResourceManager.GetString(key);
    }

    public object ConvertBack(object? value, Type? targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotSupportedException();
    }

    private string GetResourceKey(string pattern, string typeName, string valueName)
    {
        return pattern
            .Replace(TypeNamePlaceholder, typeName)
            .Replace(ValueNamePlaceholder, valueName);
    }
}
