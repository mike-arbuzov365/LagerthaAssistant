namespace LagerthaAssistant.Application.Tests.Navigation;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Navigation;
using Xunit;

public sealed class NavigationRouterTests
{
    [Fact]
    public void Resolve_ShouldRouteToCallback_WhenCallbackDataPresent()
    {
        var sut = new NavigationRouter();
        var labels = new MainMenuLabels("🗣 Chat", "📚 Vocabulary", "🛒 Shopping", "🍽 Menu");

        var route = sut.Resolve(new NavigationRouteInput("ignored", "vocab:add", NavigationSections.Main), labels);

        Assert.Equal(NavigationRouteKind.Callback, route.Kind);
        Assert.Equal("vocab:add", route.CallbackData);
    }

    [Fact]
    public void Resolve_ShouldRouteSectionText_WhenNoButtonsMatched()
    {
        var sut = new NavigationRouter();
        var labels = new MainMenuLabels("🗣 Chat", "📚 Vocabulary", "🛒 Shopping", "🍽 Menu");

        var route = sut.Resolve(new NavigationRouteInput("prepare", null, NavigationSections.Vocabulary), labels);

        Assert.Equal(NavigationRouteKind.VocabularyText, route.Kind);
    }
}
