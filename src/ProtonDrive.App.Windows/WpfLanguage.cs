using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Markup;

namespace ProtonDrive.App.Windows;

// Adapted from https://github.com/dotnet/wpf/issues/1946#issuecomment-534564980
internal static class WpfLanguage
{
    public static void InitializeToCurrentCulture()
    {
        // Create a made-up IETF language tag "more specific" than the culture we are based on.
        // This allows all standard logic regarding IETF language tag hierarchy to still make sense and we are
        // compatible with the fact that we may have overridden language specific defaults with Windows OS settings.
        var culture = CultureInfo.CurrentCulture;
        var language = XmlLanguage.GetLanguage(culture.IetfLanguageTag + "-current");
        var type = typeof(XmlLanguage);
        const BindingFlags kField = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        type.GetField("_equivalentCulture", kField)!.SetValue(language, culture);
        type.GetField("_compatibleCulture", kField)!.SetValue(language, culture);

        if (culture.IsNeutralCulture)
        {
            culture = CultureInfo.CreateSpecificCulture(culture.Name);
        }

        type.GetField("_specificCulture", kField)!.SetValue(language, culture);

        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(language));
    }
}
