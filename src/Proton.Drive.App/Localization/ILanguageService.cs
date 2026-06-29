namespace Proton.Drive.App.Localization;

public interface ILanguageService
{
    Language CurrentLanguage { get; set; }
    bool HasLanguageChanged { get; }
    IEnumerable<Language> GetSupportedLanguages();
}
