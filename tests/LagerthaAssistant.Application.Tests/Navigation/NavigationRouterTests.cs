namespace LagerthaAssistant.Application.Tests.Navigation;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Navigation;
using Xunit;

public sealed class NavigationRouterTests
{
    private static readonly MainMenuLabels EnLabels = new("Chat", "Vocabulary", "Shopping", "Menu");
    private static readonly MainMenuLabels UkLabels = new("ChatUk", "VocabularyUk", "ShoppingUk", "MenuUk");

    [Fact]
    public void Resolve_ShouldRouteToCallback_WhenCallbackDataPresent()
    {
        var sut = new NavigationRouter();

        var route = sut.Resolve(new NavigationRouteInput("ignored", "vocab:add", NavigationSections.Main), EnLabels);

        Assert.Equal(NavigationRouteKind.Callback, route.Kind);
        Assert.Equal("vocab:add", route.CallbackData);
    }

    [Theory]
    [InlineData("nav:main")]
    [InlineData("vocab:add")]
    [InlineData("vocab:list")]
    [InlineData("shop:add")]
    [InlineData("shop:list")]
    [InlineData("weekly:view")]
    [InlineData("weekly:plan")]
    [InlineData("unknown:xyz")]
    public void Resolve_ShouldKeepCallbackPayload_ForAnyCallback(string callbackData)
    {
        var sut = new NavigationRouter();

        var route = sut.Resolve(new NavigationRouteInput(null, callbackData, NavigationSections.Main), EnLabels);

        Assert.Equal(NavigationRouteKind.Callback, route.Kind);
        Assert.Equal(callbackData, route.CallbackData);
    }

    [Theory]
    [InlineData("Chat", NavigationRouteKind.MainChatButton)]
    [InlineData("Vocabulary", NavigationRouteKind.MainVocabularyButton)]
    [InlineData("Shopping", NavigationRouteKind.MainShoppingButton)]
    [InlineData("Menu", NavigationRouteKind.MainWeeklyMenuButton)]
    public void Resolve_ShouldRouteMainButtons_ForEnglishLocale(string text, NavigationRouteKind expectedKind)
    {
        var sut = new NavigationRouter();
        var route = sut.Resolve(new NavigationRouteInput(text, null, NavigationSections.Main), EnLabels);
        Assert.Equal(expectedKind, route.Kind);
    }

    [Theory]
    [InlineData("ChatUk", NavigationRouteKind.MainChatButton)]
    [InlineData("VocabularyUk", NavigationRouteKind.MainVocabularyButton)]
    [InlineData("ShoppingUk", NavigationRouteKind.MainShoppingButton)]
    [InlineData("MenuUk", NavigationRouteKind.MainWeeklyMenuButton)]
    public void Resolve_ShouldRouteMainButtons_ForUkrainianLocale(string text, NavigationRouteKind expectedKind)
    {
        var sut = new NavigationRouter();
        var route = sut.Resolve(new NavigationRouteInput(text, null, NavigationSections.Main), UkLabels);
        Assert.Equal(expectedKind, route.Kind);
    }

    [Theory]
    [InlineData(NavigationSections.Vocabulary, "ephemeral", NavigationRouteKind.VocabularyText)]
    [InlineData(NavigationSections.Shopping, "buy milk", NavigationRouteKind.ShoppingText)]
    [InlineData(NavigationSections.WeeklyMenu, "plan food", NavigationRouteKind.WeeklyMenuText)]
    [InlineData(NavigationSections.Main, "hello", NavigationRouteKind.DefaultText)]
    [InlineData(null, "hello", NavigationRouteKind.DefaultText)]
    [InlineData("stale-value", "hello", NavigationRouteKind.DefaultText)]
    public void Resolve_ShouldRouteByCurrentSection_ForFreeText(string? section, string text, NavigationRouteKind expectedKind)
    {
        var sut = new NavigationRouter();
        var route = sut.Resolve(new NavigationRouteInput(text, null, section), EnLabels);
        Assert.Equal(expectedKind, route.Kind);
    }

    [Fact]
    public void Resolve_ShouldHandleStartCommand()
    {
        var sut = new NavigationRouter();
        var route = sut.Resolve(new NavigationRouteInput("/start", null, NavigationSections.Main), EnLabels);
        Assert.Equal(NavigationRouteKind.Start, route.Kind);
    }
}
