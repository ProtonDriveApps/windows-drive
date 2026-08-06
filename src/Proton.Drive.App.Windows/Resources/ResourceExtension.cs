using System.Windows.Markup;

namespace Proton.Drive.App.Windows.Resources;

internal sealed class ResourceExtension : MarkupExtension
{
    public ResourceExtension(string key)
    {
        Key = key;
    }

    public string Key { get; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return Strings.ResourceManager.GetString(Key, Strings.Culture) ?? $"[{Key}]";
    }
}
