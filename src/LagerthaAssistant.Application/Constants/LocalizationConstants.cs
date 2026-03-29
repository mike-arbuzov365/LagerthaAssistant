namespace LagerthaAssistant.Application.Constants;

public static class LocalizationConstants
{
    public const string LocaleMemoryKey = "locale";
    public const string LocaleSelectedManuallyMemoryKey = "locale_selected_manually";
    public const string EnglishLocale = "en";
    public const string UkrainianLocale = "uk";

    public static string NormalizeLocaleCode(string? locale)
    {
        var normalized = locale?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return EnglishLocale;
        }

        if (normalized.StartsWith("ru", StringComparison.Ordinal)
            || normalized.StartsWith("be", StringComparison.Ordinal)
            || normalized.StartsWith(UkrainianLocale, StringComparison.Ordinal))
        {
            return UkrainianLocale;
        }

        return EnglishLocale;
    }
}
