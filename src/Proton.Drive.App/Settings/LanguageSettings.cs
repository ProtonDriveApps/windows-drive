namespace Proton.Drive.App.Settings;

public sealed class LanguageSettings
{
    public LanguageSettings(string cultureName)
    {
        CultureName = cultureName;
    }

    public string CultureName { get; }
}
