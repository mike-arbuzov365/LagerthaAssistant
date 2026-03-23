using LagerthaAssistant.Application.Constants;
using System.Text.RegularExpressions;

namespace LagerthaAssistant.Application.Navigation;

public sealed class NavigationRouter
{
    private static readonly Regex LeadingDecorationRegex = new("^[^\\p{L}\\p{N}]+", RegexOptions.Compiled);

    public NavigationRoute Resolve(NavigationRouteInput input, MainMenuLabels labels)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(labels);

        if (!string.IsNullOrWhiteSpace(input.CallbackData))
        {
            return new NavigationRoute(NavigationRouteKind.Callback, input.CallbackData.Trim());
        }

        var normalizedText = input.Text?.Trim();
        if (string.Equals(normalizedText, "/start", StringComparison.OrdinalIgnoreCase))
        {
            return new NavigationRoute(NavigationRouteKind.Start);
        }

        if (IsLabelMatch(normalizedText, labels.Chat))
        {
            return new NavigationRoute(NavigationRouteKind.MainChatButton);
        }

        if (IsLabelMatch(normalizedText, labels.Vocabulary))
        {
            return new NavigationRoute(NavigationRouteKind.MainVocabularyButton);
        }

        if (IsLabelMatch(normalizedText, labels.Shopping))
        {
            return new NavigationRoute(NavigationRouteKind.MainShoppingButton);
        }

        if (IsLabelMatch(normalizedText, labels.WeeklyMenu))
        {
            return new NavigationRoute(NavigationRouteKind.MainWeeklyMenuButton);
        }

        if (IsLabelMatch(normalizedText, labels.Settings))
        {
            return new NavigationRoute(NavigationRouteKind.MainSettingsButton);
        }

        var section = NavigationSections.Normalize(input.CurrentSection);
        return section switch
        {
            NavigationSections.Chat => new NavigationRoute(NavigationRouteKind.ChatText),
            NavigationSections.Vocabulary => new NavigationRoute(NavigationRouteKind.VocabularyText),
            NavigationSections.Shopping => new NavigationRoute(NavigationRouteKind.ShoppingText),
            NavigationSections.Inventory => new NavigationRoute(NavigationRouteKind.InventoryText),
            NavigationSections.WeeklyMenu => new NavigationRoute(NavigationRouteKind.WeeklyMenuText),
            NavigationSections.Settings => new NavigationRoute(NavigationRouteKind.SettingsText),
            NavigationSections.LanguageOnboarding => new NavigationRoute(NavigationRouteKind.LanguageOnboardingText),
            _ => new NavigationRoute(NavigationRouteKind.DefaultText)
        };
    }

    private static bool IsLabelMatch(string? text, string label)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (string.Equals(text, label, StringComparison.Ordinal))
        {
            return true;
        }

        var plainLabel = StripLeadingDecorations(label);
        if (string.Equals(text, plainLabel, StringComparison.Ordinal))
        {
            return true;
        }

        var plainText = StripLeadingDecorations(text);
        return string.Equals(plainText, plainLabel, StringComparison.Ordinal);
    }

    private static string StripLeadingDecorations(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return LeadingDecorationRegex.Replace(value.Trim(), string.Empty).Trim();
    }
}

public sealed record NavigationRouteInput(
    string? Text,
    string? CallbackData,
    string? CurrentSection);

public sealed record MainMenuLabels(
    string Chat,
    string Vocabulary,
    string Shopping,
    string WeeklyMenu,
    string Settings);

public sealed record NavigationRoute(
    NavigationRouteKind Kind,
    string? CallbackData = null);

public enum NavigationRouteKind
{
    Start = 0,
    Callback = 1,
    MainChatButton = 2,
    MainVocabularyButton = 3,
    MainShoppingButton = 4,
    MainWeeklyMenuButton = 5,
    MainSettingsButton = 6,
    ChatText = 7,
    VocabularyText = 8,
    ShoppingText = 9,
    InventoryText = 14,
    WeeklyMenuText = 10,
    SettingsText = 11,
    LanguageOnboardingText = 12,
    DefaultText = 13
}
