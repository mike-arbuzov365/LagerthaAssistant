namespace LagerthaAssistant.Application.Constants;

public static class LocalizationConstants
{
    public const string LocaleMemoryKey = "locale";
    public const string LocaleSelectedManuallyMemoryKey = "locale_selected_manually";
    public const string EnglishLocale = "en";
    public const string UkrainianLocale = "uk";
    public const string SpanishLocale = "es";
    public const string FrenchLocale = "fr";
    public const string GermanLocale = "de";
    public const string PolishLocale = "pl";

    public static string NormalizeLocaleCode(string? locale)
    {
        var normalized = locale?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return EnglishLocale;
        }

        if (normalized.StartsWith("be", StringComparison.Ordinal)
            || normalized.StartsWith(UkrainianLocale, StringComparison.Ordinal))
        {
            return UkrainianLocale;
        }

        if (normalized.StartsWith(SpanishLocale, StringComparison.Ordinal))
        {
            return SpanishLocale;
        }

        if (normalized.StartsWith(FrenchLocale, StringComparison.Ordinal))
        {
            return FrenchLocale;
        }

        if (normalized.StartsWith(GermanLocale, StringComparison.Ordinal))
        {
            return GermanLocale;
        }

        if (normalized.StartsWith(PolishLocale, StringComparison.Ordinal))
        {
            return PolishLocale;
        }

        return EnglishLocale;
    }
}
