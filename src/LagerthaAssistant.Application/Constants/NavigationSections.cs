namespace LagerthaAssistant.Application.Constants;

public static class NavigationSections
{
    public const string Main = "main";
    public const string Vocabulary = "vocabulary";
    public const string Shopping = "shopping";
    public const string WeeklyMenu = "weekly_menu";
    public const string Settings = "settings";
    public const string LanguageOnboarding = "language_onboarding";

    public static bool IsKnown(string? section)
    {
        if (string.IsNullOrWhiteSpace(section))
        {
            return false;
        }

        return section.Equals(Main, StringComparison.OrdinalIgnoreCase)
            || section.Equals(Vocabulary, StringComparison.OrdinalIgnoreCase)
            || section.Equals(Shopping, StringComparison.OrdinalIgnoreCase)
            || section.Equals(WeeklyMenu, StringComparison.OrdinalIgnoreCase)
            || section.Equals(Settings, StringComparison.OrdinalIgnoreCase)
            || section.Equals(LanguageOnboarding, StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string? section)
    {
        if (string.IsNullOrWhiteSpace(section))
        {
            return Main;
        }

        var normalized = section.Trim().ToLowerInvariant();
        return IsKnown(normalized)
            ? normalized
            : Main;
    }
}
