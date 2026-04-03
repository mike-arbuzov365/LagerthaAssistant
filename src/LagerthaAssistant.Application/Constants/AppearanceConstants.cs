namespace LagerthaAssistant.Application.Constants;

public static class AppearanceConstants
{
    public const string ThemeModeMemoryKey = "theme_mode";
    public const string ThemeModeSystem = "system";
    public const string ThemeModeLight = "light";
    public const string ThemeModeDark = "dark";

    public static readonly IReadOnlyList<string> SupportedThemeModes =
    [
        ThemeModeSystem,
        ThemeModeLight,
        ThemeModeDark
    ];

    public static string NormalizeThemeMode(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        return normalized switch
        {
            ThemeModeLight => ThemeModeLight,
            ThemeModeDark => ThemeModeDark,
            _ => ThemeModeSystem
        };
    }
}
