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

        var section = NavigationSections.Normalize(input.CurrentSection);
        return section switch
        {
            NavigationSections.Vocabulary => new NavigationRoute(NavigationRouteKind.VocabularyText),
            NavigationSections.Shopping => new NavigationRoute(NavigationRouteKind.ShoppingText),
            NavigationSections.WeeklyMenu => new NavigationRoute(NavigationRouteKind.WeeklyMenuText),
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
    string WeeklyMenu);

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
    VocabularyText = 6,
    ShoppingText = 7,
    WeeklyMenuText = 8,
    DefaultText = 9
}
