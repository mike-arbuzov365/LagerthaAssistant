namespace LagerthaAssistant.IntegrationTests.Services;

using LagerthaAssistant.Api.Services;
using LagerthaAssistant.Infrastructure.Services;
using Xunit;

public sealed class TelegramNavigationPresenterTests
{
    [Fact]
    public void BuildMainReplyKeyboard_ShouldIncludeSettingsRow()
    {
        var sut = new TelegramNavigationPresenter(new LocalizationService());

        var keyboard = sut.BuildMainReplyKeyboard("en");

        Assert.Equal(3, keyboard.Keyboard.Count);
        Assert.Single(keyboard.Keyboard[2]);
        Assert.Contains("Settings", keyboard.Keyboard[2][0].Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildMainReplyKeyboard_ShouldUseEnglishLabels_ForEnglishLocale()
    {
        var sut = new TelegramNavigationPresenter(new LocalizationService());

        var keyboard = sut.BuildMainReplyKeyboard("en");
        var labels = keyboard.Keyboard
            .SelectMany(row => row)
            .Select(button => button.Text)
            .ToList();

        Assert.Contains(labels, x => x.Contains("Vocabulary", StringComparison.Ordinal));
        Assert.DoesNotContain(labels, x => x.Contains("Словник", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildOnboardingLanguageKeyboard_ShouldIncludeCombinedDePlButton()
    {
        var sut = new TelegramNavigationPresenter(new LocalizationService());

        var keyboard = sut.BuildOnboardingLanguageKeyboard("en");
        var lastRow = Assert.Single(keyboard.InlineKeyboard.Skip(2).Take(1));
        var button = Assert.Single(lastRow);

        Assert.Equal("lang:de_pl", button.CallbackData);
    }

    [Fact]
    public void BuildSettingsKeyboard_ShouldContainExpectedCallbacks()
    {
        var sut = new TelegramNavigationPresenter(new LocalizationService());

        var keyboard = sut.BuildSettingsKeyboard("en");
        var callbacks = keyboard.InlineKeyboard
            .SelectMany(row => row)
            .Select(button => button.CallbackData)
            .ToList();

        Assert.Contains("settings:language", callbacks);
        Assert.Contains("settings:savemode", callbacks);
        Assert.Contains("settings:onedrive", callbacks);
        Assert.Contains("settings:notion", callbacks);
        Assert.Contains("nav:main", callbacks);
    }

    [Fact]
    public void BuildVocabularyKeyboard_ShouldPlaceBatchBeforeStatistics()
    {
        var sut = new TelegramNavigationPresenter(new LocalizationService());

        var keyboard = sut.BuildVocabularyKeyboard("uk");

        Assert.Equal("vocab:add", keyboard.InlineKeyboard[0][0].CallbackData);
        Assert.Equal("vocab:batch", keyboard.InlineKeyboard[0][1].CallbackData);
        Assert.Equal("vocab:url", keyboard.InlineKeyboard[1][0].CallbackData);
        Assert.Equal("vocab:stats", keyboard.InlineKeyboard[1][1].CallbackData);
        Assert.Contains("Стат", keyboard.InlineKeyboard[1][1].Text, StringComparison.OrdinalIgnoreCase);
    }
}
