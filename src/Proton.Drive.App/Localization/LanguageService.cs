using System.Globalization;
using Microsoft.Extensions.Logging;
using Proton.Drive.App.Settings;
using Proton.Drive.Shared.Localization;
using Proton.Drive.Shared.Repository;

namespace Proton.Drive.App.Localization;

public sealed class LanguageService : ILanguageService, ILanguageProvider
{
    internal static readonly HashSet<string> SupportedLanguages =
    [
        "en",
        "de",
        "fr",
        "nl",
        "es",
        "es-419",
        "it",
        "pl",
        "pt-BR",
        "ru",
        "tr",
        "ca-ES",
        "cs-CZ",
        "da-DK",
        "hu",
        "id",
        "nb-NO",
        "pt",
        "ro",
        "sk",
        "sv-SE",
        "el-GR",
        "be-BY",
        "zh-CN",
        "zh-TW",
    ];

    internal static readonly Dictionary<string, string> RegionalLanguageMapping = new()
    {
        { "es-AR", "es-419" },
        { "es-BO", "es-419" },
        { "es-CL", "es-419" },
        { "es-CO", "es-419" },
        { "es-CR", "es-419" },
        { "es-CU", "es-419" },
        { "es-DO", "es-419" },
        { "es-EC", "es-419" },
        { "es-GT", "es-419" },
        { "es-HN", "es-419" },
        { "es-MX", "es-419" },
        { "es-NI", "es-419" },
        { "es-PA", "es-419" },
        { "es-PY", "es-419" },
        { "es-PE", "es-419" },
        { "es-PR", "es-419" },
        { "es-SV", "es-419" },
        { "es-UY", "es-419" },
        { "es-VE", "es-419" },
    };

    private readonly IRepository<LanguageSettings> _repository;
    private readonly ILogger<LanguageService> _logger;
    private readonly Lazy<Language> _autoLanguage;
    private readonly Lazy<Language> _initialLanguage;

    private Language? _currentLanguage;

    public LanguageService(IRepository<LanguageSettings> repository, ILogger<LanguageService> logger)
    {
        _repository = repository;
        _logger = logger;

        _autoLanguage = new Lazy<Language>(GetAutoLanguage);
        _initialLanguage = new Lazy<Language>(GetInitialLanguage);
    }

    public Language CurrentLanguage
    {
        get => _currentLanguage ??= _initialLanguage.Value;
        set
        {
            _currentLanguage = value;
            _repository.Set(value.IsAuto ? null : new LanguageSettings(cultureName: value.CultureName));
            _logger.LogInformation("App language changed to {Language}", GetLanguageNameForLogging(value));
        }
    }

    public bool HasLanguageChanged => CurrentLanguage.CultureName != _initialLanguage.Value.CultureName;

    public IEnumerable<Language> GetSupportedLanguages()
    {
        var supportedLanguages = SupportedLanguages
            .Select(name => new Language(GetCapitalizedCultureNativeName(new CultureInfo(name)), name))
            .OrderBy(x => x.DisplayName)
            .Prepend(_autoLanguage.Value);

        return supportedLanguages;
    }

    public string GetCulture()
    {
        // Auto language must be initialized before returning, because the caller might update the
        // CultureInfo.CurrentUICulture used for obtaining the auto language.
        _ = _autoLanguage.Value;

        return CurrentLanguage.CultureName;
    }

    private static CultureInfo? GetCultureInfo(string cultureName)
    {
        try
        {
            return new CultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    private static string GetCapitalizedCultureNativeName(CultureInfo culture)
    {
        return Capitalize(culture.NativeName, culture);
    }

    private static string Capitalize(string input, CultureInfo culture)
    {
        return !string.IsNullOrEmpty(input)
            ? string.Concat(char.ToUpper(input[0], culture).ToString(), input.AsSpan(1))
            : string.Empty;
    }

    private static string GetLanguageNameForLogging(Language language)
    {
        return language.IsAuto ? $"Auto (\"{language.CultureName}\")" : $"\"{language.CultureName}\"";
    }

    private static Language? GetLanguage(string? cultureName)
    {
        if (cultureName is null)
        {
            return null;
        }

        var culture = GetCultureInfo(cultureName);

        if (culture is null)
        {
            return null;
        }

        var supportedLanguageCulture = GetSupportedLanguageCulture(culture);

        return new Language(GetCapitalizedCultureNativeName(supportedLanguageCulture), supportedLanguageCulture.Name);
    }

    private static CultureInfo GetSupportedLanguageCulture(CultureInfo culture)
    {
        if (SupportedLanguages.Contains(culture.Name))
        {
            return culture;
        }

        if (RegionalLanguageMapping.TryGetValue(culture.Name, out var regionalCultureName) && SupportedLanguages.Contains(regionalCultureName))
        {
            return new CultureInfo(regionalCultureName);
        }

        return !culture.IsNeutralCulture
            ? GetSupportedLanguageCulture(culture.Parent)
            : new CultureInfo("en");
    }

    private Language GetAutoLanguage()
    {
        var systemUICulture = CultureInfo.CurrentUICulture;
        var autoCulture = GetSupportedLanguageCulture(systemUICulture);

        _logger.LogInformation("System UI language is \"{SystemUILanguage}\", app auto language is \"{AutoLanguage}\"", systemUICulture.Name, autoCulture.Name);

        return new Language($"Auto ({GetCapitalizedCultureNativeName(autoCulture)})", autoCulture.Name, IsAuto: true);
    }

    private Language GetInitialLanguage()
    {
        var result = GetLanguage(_repository.Get()?.CultureName) ?? _autoLanguage.Value;

        _logger.LogInformation("App language is {Language}", GetLanguageNameForLogging(result));

        return result;
    }
}
