using LagerthaAssistant.Application.Constants;

namespace LagerthaAssistant.Application.Navigation;

public sealed class NavigationRouter
{
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

        if (string.Equals(normalizedText, labels.Chat, StringComparison.Ordinal))
        {
            return new NavigationRoute(NavigationRouteKind.MainChatButton);
        }

        if (string.Equals(normalizedText, labels.Vocabulary, StringComparison.Ordinal))
        {
            return new NavigationRoute(NavigationRouteKind.MainVocabularyButton);
        }

        if (string.Equals(normalizedText, labels.Shopping, StringComparison.Ordinal))
        {
            return new NavigationRoute(NavigationRouteKind.MainShoppingButton);
        }

        if (string.Equals(normalizedText, labels.WeeklyMenu, StringComparison.Ordinal))
        {
            return new NavigationRoute(NavigationRouteKind.MainWeeklyMenuButton);
        }

        if (string.Equals(normalizedText, labels.Settings, StringComparison.Ordinal))
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
