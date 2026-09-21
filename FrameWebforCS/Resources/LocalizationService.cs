using System.Globalization;
using System.Resources;

namespace FrameWebforCS.Resources;

public enum UiLanguage
{
    Japanese,
    English,
    Chinese,
}

public sealed class LocalizationService
{
    private const string ResourceBaseName = "FrameWebforCS.Resources.Strings";
    private static readonly ResourceManager ResourceManager =
        new(ResourceBaseName, typeof(LocalizationService).Assembly);

    private CultureInfo _culture;

    public LocalizationService()
        : this(ResolveInitialLanguage(CultureInfo.CurrentUICulture))
    {
    }

    public LocalizationService(UiLanguage language)
    {
        Language = language;
        _culture = CreateCulture(language);
    }

    public event EventHandler? CultureChanged;

    public UiLanguage Language { get; private set; }

    public CultureInfo Culture => _culture;

    public string this[string key]
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return ResourceManager.GetString(key, _culture)
                ?? throw new MissingManifestResourceException(
                    $"The localized resource key '{key}' is missing for culture '{_culture.Name}'.");
        }
    }

    public void SetLanguage(UiLanguage language)
    {
        if (language == Language)
        {
            return;
        }

        Language = language;
        _culture = CreateCulture(language);
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    public static CultureInfo CreateCulture(UiLanguage language) => language switch
    {
        UiLanguage.Japanese => CultureInfo.GetCultureInfo("ja"),
        UiLanguage.English => CultureInfo.GetCultureInfo("en"),
        UiLanguage.Chinese => CultureInfo.GetCultureInfo("zh"),
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
    };

    private static UiLanguage ResolveInitialLanguage(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName switch
        {
            "ja" => UiLanguage.Japanese,
            "zh" => UiLanguage.Chinese,
            _ => UiLanguage.English,
        };
}
